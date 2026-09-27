#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace GoDeep
{
    public sealed class NetworkSmokeClientComponent : MonoBehaviour
    {
        [Serializable]
        private sealed class Report
        {
            public bool connected; //실제 연결 성공 여부
            public string status; //연결 결과 안내
            public int players; //관측한 방 인원
            public int localOwners; //자기 상태 권한을 가진 다이버 수
            public int remoteDivers; //관측한 원격 다이버 수
            public float localZ; //이동 이후 자기 진행 위치
            public float localYaw; //이동 이후 자기 회전
            public string[] observedPositions; //각 참가자의 수신 위치
            public string phase; //현재 검사 진행 단계
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void launchIfRequested() //개발 빌드의 명시적인 검사 인수가 있을 때만 독립 클라이언트 실행
        {
            if (Application.isEditor || !Debug.isDebugBuild) return;
            var arguments = Environment.GetCommandLineArgs(); //실행 파일에 전달된 검사 인수
            var roomIndex = Array.IndexOf(arguments, "-godeepSmokeRoom"); //방 코드 인수 위치
            if (roomIndex < 0 || roomIndex + 1 >= arguments.Length) return;
            var room = arguments[roomIndex + 1]; //입장할 임시 검사 방
            if (!NetworkSession.isValidRoomCode(room)) return;
            var idIndex = Array.IndexOf(arguments, "-godeepSmokeId"); //검사 결과 파일 번호 인수 위치
            var id = idIndex >= 0 && idIndex + 1 < arguments.Length && int.TryParse(arguments[idIndex + 1], out var parsed) ? Mathf.Clamp(parsed, 1, 8) : 1; //허용 범위로 제한할 검사 번호
            var component = new GameObject("Development Network Smoke Client").AddComponent<NetworkSmokeClientComponent>(); //개발 검사 전용 실행기
            DontDestroyOnLoad(component.gameObject);
            component.StartCoroutine(component.run(room, id));
        }

        private IEnumerator run(string room, int id) //입장 후 실제 입력 장치로 이동하고 수신 상태를 기록
        {
            var report = new Report { phase = "starting" }; //현재 검사 결과
            var reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "smoke-" + id + ".json")); //실행 파일 옆의 한정된 결과 경로
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            yield return SceneManager.LoadSceneAsync("StandBy");
            var session = NetworkSession.instance; //실제 UI와 같은 연결 진입점
            var connection = session.joinRoom(room); //실제 Photon 입장 요청
            while (!connection.IsCompleted) yield return null;
            report.connected = connection.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && connection.Result;
            report.status = session.status;
            if (!report.connected)
            {
                report.phase = "rejected";
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                Application.Quit(0);
                yield break;
            }
            var deadline = Time.realtimeSinceStartup + 15f; //스폰 대기 제한 시각
            while (session.localPlayer == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (session.localPlayer == null)
            {
                report.phase = "spawn-timeout";
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                Application.Quit(1);
                yield break;
            }
            var keyboard = InputSystem.AddDevice<Keyboard>(); //실제 입력 경로를 구동할 검사 장치
            var mouse = InputSystem.AddDevice<Mouse>(); //시점 동기화 검사 장치
            session.localPlayer.setPaused(false);
            var started = Time.realtimeSinceStartup; //동작 검사 시작 시각
            var nextReport = 0f; //다음 결과 저장 시각
            while (Time.realtimeSinceStartup - started < 150f && session.state == NetworkSession.ConnectionState.Connected)
            {
                var elapsed = Time.realtimeSinceStartup - started; //검사 시작 후 지난 시간
                InputSystem.QueueStateEvent(keyboard, elapsed < 3f ? new KeyboardState(Key.W) : new KeyboardState());
                if (elapsed < 0.5f) InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(4f, -1f));
                if (Time.realtimeSinceStartup >= nextReport)
                {
                    var divers = FindObjectsByType<NetworkDiverComponent>(); //이 프로세스가 실제 수신한 다이버 목록
                    report.players = session.playerCount;
                    report.localOwners = divers.Count(item => item.HasStateAuthority);
                    report.remoteDivers = divers.Count(item => !item.HasStateAuthority);
                    report.localZ = session.localPlayer != null ? session.localPlayer.transform.position.z : 0f;
                    report.localYaw = session.localPlayer != null ? session.localPlayer.transform.eulerAngles.y : 0f;
                    report.observedPositions = divers.Select(item => item.Object.StateAuthority.RawEncoded + ": " + item.transform.position.ToString("F2") + " yaw=" + item.transform.eulerAngles.y.ToString("F1") + " pitch=" + item.viewPitch.ToString("F1")).ToArray();
                    report.phase = "observing";
                    File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                    nextReport = Time.realtimeSinceStartup + 1f;
                }
                yield return null;
            }
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            var leave = session.leaveRoom(); //검사 종료 시 임시 방 정리
            while (!leave.IsCompleted) yield return null;
            report.phase = "finished";
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.Quit(0);
        }
    }
}
#endif
