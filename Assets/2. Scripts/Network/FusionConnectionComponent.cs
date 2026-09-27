using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoDeep
{
    public sealed class FusionConnectionComponent : MonoBehaviour
    {
        [SerializeField] private NetworkObject diverPrefab; //동기화할 다이버 프리팹
        [SerializeField] private string fixedRegion = "asia"; //방 코드가 같은 지역에서 해석되도록 고정할 서버
        [SerializeField] private string networkVersion = "godeep-m2-01"; //호환되는 시제품끼리 연결할 버전
        [SerializeField, Min(5f)] private float connectionTimeout = 35f; //접속 대기 제한 시간
        private NetworkSession session; //외부 기능 진입점
        private NetworkRunner runner; //현재 연결에만 사용하는 Fusion 러너
        private NetworkSceneManagerDefault sceneManager; //네트워크 씬 이동 구성 요소
        private CancellationTokenSource connectionCancellation; //접속 제한 시간과 파괴 취소 처리
        private bool disconnecting; //정상 종료 콜백의 중복 처리 방지
        private string activeRoomCode = ""; //현재 연결 요청의 방 코드
        private string spawnError; //플레이어 생성 실패 안내

        public string roomCode => activeRoomCode; //메뉴에 전달할 코드
        public string region => fixedRegion; //메뉴에 전달할 서버 지역
        public int playerCount => runner != null && runner.IsRunning ? runner.ActivePlayers.Count() : 0; //현재 접속 인원
        public Player localPlayer => runner != null && runner.IsRunning && runner.TryGetPlayerObject(runner.LocalPlayer, out var diver)
            ? diver.GetComponent<Player>() : null; //러너가 소유한 자기 플레이어

        public void initialize(NetworkSession owner) //파사드 연결
        {
            session = owner;
        }

        public async Task<string> connect(string code, bool create) //SDK 설정을 복사하여 실제 방 연결 시작
        {
            if (diverPrefab == null) return "Network diver is not configured.";
            if (PhotonAppSettings.Global == null || !Guid.TryParse(PhotonAppSettings.Global.AppSettings.AppIdFusion, out _))
                return "Set a valid Fusion App ID in local Photon settings.";
            if (!Application.CanStreamedLevelBeLoaded("Play")) return "Play scene is missing from Build Settings.";
            activeRoomCode = code;
            spawnError = null;
            var runnerObject = new GameObject("GoDeep Fusion Runner"); //재접속 때 새로 만드는 러너 오브젝트
            runnerObject.transform.SetParent(transform, false);
            runner = runnerObject.AddComponent<NetworkRunner>();
            runner.ProvideInput = false;
            sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
            runner.AddCallbacks(new NetworkDelegates
            {
                OnSceneLoadDone = onSceneReady,
                OnShutdown = onShutdown
            });
            var settings = PhotonAppSettings.Global.AppSettings.GetCopy(); //저장된 App ID를 변경하지 않는 연결용 설정
            settings.FixedRegion = fixedRegion;
            settings.AppVersion = networkVersion;
            var sceneInfo = new NetworkSceneInfo(); //방 참가자에게 공통으로 적용할 씬
            sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath("Assets/1. Scenes/Play.unity")), LoadSceneMode.Single);
            connectionCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(connectionTimeout));
            try
            {
                var result = await runner.StartGame(new StartGameArgs //친구 코드 기반 비공개 목록 방 설정
                {
                    GameMode = GameMode.Shared,
                    SessionName = code,
                    PlayerCount = 4,
                    IsVisible = false,
                    IsOpen = true,
                    EnableClientSessionCreation = create,
                    CustomPhotonAppSettings = settings,
                    Scene = sceneInfo,
                    SceneManager = sceneManager,
                    StartGameCancellationToken = connectionCancellation.Token
                });
                if (result.Ok && spawnError == null) return null;
                var reason = spawnError ?? "Could not connect: " + result.ShutdownReason + ". Check the code and network, then retry."; //SDK 자격 정보를 제외한 실패 안내
                await disconnect();
                return reason;
            }
            catch (Exception exception) //SDK 예외 뒤에도 다음 연결을 시도할 수 있도록 정리
            {
                var reason = exception is OperationCanceledException ? "Connection timed out. Please retry." : "Connection failed (" + exception.GetType().Name + "). Please retry."; //상세 비밀 정보를 포함하지 않는 안내
                await disconnect();
                return reason;
            }
            finally
            {
                connectionCancellation?.Dispose();
                connectionCancellation = null;
            }
        }

        private void onSceneReady(NetworkRunner activeRunner) //동굴 로딩 완료 후 자신의 다이버를 한 번만 생성
        {
            if (activeRunner != runner || !activeRunner.IsRunning || activeRunner.TryGetPlayerObject(activeRunner.LocalPlayer, out _)) return;
            try
            {
                var slot = (activeRunner.LocalPlayer.RawEncoded - 1) % 4; //겹침을 줄일 입구 배치 번호
                var position = new Vector3((slot % 2 == 0 ? -1f : 1f), 0f, -9f + (slot / 2) * 2f); //입구의 수영 시작 위치
                var diver = activeRunner.Spawn(diverPrefab, position, Quaternion.identity, activeRunner.LocalPlayer); //상태 권한을 가진 자기 다이버
                activeRunner.SetPlayerObject(activeRunner.LocalPlayer, diver);
            }
            catch (Exception exception) //프리팹 구성 오류를 연결 결과에 전달
            {
                spawnError = "Diver spawn failed (" + exception.GetType().Name + ").";
                session.handleDisconnect(spawnError);
            }
        }

        private void onShutdown(NetworkRunner stoppedRunner, ShutdownReason reason) //예기치 않은 종료를 파사드에 전달
        {
            if (!disconnecting && stoppedRunner == runner && session != null) session.handleDisconnect(reason.ToString());
        }

        public async Task disconnect() //씬 복귀 전에 기존 러너를 완전히 정리
        {
            if (disconnecting) return;
            disconnecting = true;
            var previousRunner = runner; //이번 종료 대상으로 고정할 러너
            runner = null;
            try
            {
                if (previousRunner != null) await previousRunner.Shutdown();
            }
            catch (Exception exception) //정리 실패도 다음 시도에 남기지 않도록 안내
            {
                Debug.LogWarning("Fusion cleanup: " + exception.GetType().Name);
            }
            finally
            {
                if (previousRunner != null) Destroy(previousRunner.gameObject);
                sceneManager = null;
                activeRoomCode = "";
                disconnecting = false;
            }
        }

        private void OnDestroy() //플레이 모드 종료 중 접속 대기 취소
        {
            connectionCancellation?.Cancel();
        }
    }
}
