using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using GoDeep;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class GoDeepNetworkValidation
{
    public static async Task<object> connect() //실제 Photon 방 생성과 잘못된 코드 및 중복 요청 검증
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Start Play Mode first.");
        if (SceneManager.GetActiveScene().name != "StandBy")
        {
            SceneManager.LoadSceneAsync("StandBy");
            await waitFor(() => NetworkSession.instance != null && SceneManager.GetActiveScene().name == "StandBy", 10);
        }
        var session = NetworkSession.instance; //검사할 실제 연결 진입점
        check(!await session.joinRoom(""), "Empty code rejected");
        check(!await session.joinRoom("TOO-LONG-ROOM"), "Malformed code rejected");
        var start = session.createRoom(); //진행 중 중복 요청을 검사할 최초 접속
        check(!await session.createRoom(), "Duplicate connection rejected");
        check(await start, session.status);
        await waitFor(() => session.localPlayer != null, 15);
        check(SceneManager.GetActiveScene().name == "Play", "Network loads Play");
        check(Object.FindAnyObjectByType<PrototypeSession>().isNetworkPractice, "Online scene replaces offline controller");
        check(session.playerCount == 1, "Creator is the first player");
        check(Object.FindObjectsByType<AudioListener>().Count(item => item.isActiveAndEnabled) == 1, "Only local audio listener active");
        check(Object.FindObjectsByType<NetworkDiverComponent>().Length == 1, "One owned network diver spawned");
        Directory.CreateDirectory(".utmp/network-20260928");
        File.WriteAllText(".utmp/network-20260928/room.txt", session.roomCode);
        return new { connected = true, room = session.roomCode, players = session.playerCount, checks = 9, region = session.region };
    }

    public static object snapshot() //독립 클라이언트가 연결된 현재 방과 원격 위치 기록
    {
        var session = NetworkSession.instance; //활성 방 진입점
        return new
        {
            state = session != null ? session.state.ToString() : "No session",
            players = session != null ? session.playerCount : 0,
            listeners = Object.FindObjectsByType<AudioListener>().Count(item => item.isActiveAndEnabled),
            divers = Object.FindObjectsByType<NetworkDiverComponent>().Select(item => new
            {
                owner = item.Object.StateAuthority.RawEncoded,
                local = item.HasStateAuthority,
                position = item.transform.position.ToString("F3"),
                yaw = item.transform.eulerAngles.y,
                pitch = item.viewPitch,
                camera = item.GetComponentInChildren<Camera>() != null,
                controller = item.GetComponent<CharacterController>().enabled
            }).ToArray()
        };
    }

    public static async Task<object> capture() //온라인 장면과 UI를 함께 렌더링하여 저장
    {
        NetworkSession.instance.localPlayer.setPaused(false);
        await Task.Delay(300);
        var camera = Camera.main; //자기 다이버 시점
        var canvases = Object.FindObjectsByType<Canvas>().Where(item => item.renderMode == RenderMode.ScreenSpaceOverlay).ToArray(); //화면 캡처에 포함할 UI
        var previousCameras = canvases.Select(item => item.worldCamera).ToArray(); //복원할 UI 카메라
        var previousDistances = canvases.Select(item => item.planeDistance).ToArray(); //복원할 UI 거리
        var previousTarget = camera.targetTexture; //복원할 카메라 출력
        var previousActive = RenderTexture.active; //복원할 렌더 타깃
        var texture = new RenderTexture(1280, 720, 24); //검사 화면 렌더 타깃
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); //저장할 화면
        try
        {
            foreach (var canvas in canvases) //화면 UI도 카메라 렌더에 포함
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 0.4f;
            }
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            image.Apply();
            File.WriteAllBytes(".utmp/network-20260928/online-movement.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            for (var index = 0; index < canvases.Length; index++) //캔버스 설정 복원
            {
                canvases[index].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[index].worldCamera = previousCameras[index];
                canvases[index].planeDistance = previousDistances[index];
            }
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(texture);
            NetworkSession.instance.localPlayer.setPaused(true);
        }
        return new { screenshot = ".utmp/network-20260928/online-movement.png" };
    }

    public static async Task<object> leaveAndRetry() //퇴장 정리와 같은 진입점의 재접속 검증
    {
        var session = NetworkSession.instance; //유지되는 연결 파사드
        await session.leaveRoom();
        await waitFor(() => SceneManager.GetActiveScene().name == "StandBy", 10);
        check(Object.FindObjectsByType<NetworkRunner>().Length == 0, "Runner cleaned after leave");
        check(session.localPlayer == null && session.playerCount == 0, "No stale player after leave");
        check(!await session.joinRoom(Guid.NewGuid().ToString("N").Substring(0, 8)), "Missing room is not implicitly created");
        var missingRoomMessage = session.status; //존재하지 않는 방의 사용자 안내
        check(session.state == NetworkSession.ConnectionState.Idle, "Failed join returns to idle");
        check(await session.createRoom(), session.status);
        await waitFor(() => session.localPlayer != null, 15);
        await session.leaveRoom();
        await waitFor(() => SceneManager.GetActiveScene().name == "StandBy", 10);
        return new { leaveCleanup = true, nonexistentRoomRejected = true, failureMessage = missingRoomMessage, reconnect = true };
    }

    public static async Task<object> finish() //검사 방에서 정상 퇴장하고 러너 정리 확인
    {
        await NetworkSession.instance.leaveRoom();
        await Task.Delay(200);
        return new { state = NetworkSession.instance.state.ToString(), scene = SceneManager.GetActiveScene().name,
            runners = Object.FindObjectsByType<NetworkRunner>().Length };
    }

    public static object buildWindows() //독립 클라이언트 검증용 개발 빌드 제작
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
        Directory.CreateDirectory(".utmp/network-20260928/build");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions //기존 빌드 씬으로 Windows 실행 파일 생성
        {
            scenes = EditorBuildSettings.scenes.Where(item => item.enabled).Select(item => item.path).ToArray(),
            locationPathName = ".utmp/network-20260928/build/GoDeep.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        return new { result = result.summary.result.ToString(), errors = result.summary.totalErrors, warnings = result.summary.totalWarnings, bytes = result.summary.totalSize };
    }

    private static async Task waitFor(Func<bool> condition, int seconds) //실제 비동기 상태를 제한 시간 안에서 기다림
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds); //검사 제한 시각
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Network validation timed out.");
            await Task.Delay(100);
        }
    }

    private static void check(bool condition, string message) //실패한 수용 조건 즉시 보고
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
