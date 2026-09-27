using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GoDeep;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GoDeepPrototypeValidation
{
    private static readonly List<string> passed = new List<string>(); //통과한 실제 동작 검사

    public static async Task<object> run() //실제 플레이 모드에서 메뉴와 수영과 생존 흐름 검증
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Start Play Mode in Home first.");
        passed.Clear();
        check(SceneManager.GetActiveScene().name == "Home", "Home entry scene");
        capture("home-ui");
        click("PREPARE TO DIVE  >");
        await waitForScene("StandBy");
        check(true, "Home -> StandBy persistent button binding");
        capture("standby-ui");
        click("START LOCAL PRACTICE");
        await waitForScene("Play");
        check(true, "StandBy -> Play persistent button binding");
        await Task.Delay(200);
        var session = Object.FindAnyObjectByType<PrototypeSession>(); //현재 시제품 세션
        var player = session.player; //실제 씬 플레이어
        var oxygen = player.GetComponent<PlayerOxygenComponent>(); //검사 대상 산소 기능
        var watch = player.GetComponent<PlayerWatchComponent>(); //검사 대상 워치 기능
        var dummy = GameObject.Find("Training Diver").GetComponent<Player>(); //실제 산소 공유 대상
        var movement = player.GetComponent<PlayerMovementComponent>(); //충돌 이동 기능
        var controller = player.GetComponent<CharacterController>(); //검사할 충돌 캡슐
        session.enabled = false;
        try
        {
            player.setPaused(false);
            check(player.isInAirPocket && player.oxygenPercent == 100f && player.packCount == 2, "Fresh run starts in air with oxygen and two packs");
            player.processInput(new PlayerInputComponent.Frame { switchHand = true }, 0.02f);
            player.processInput(new PlayerInputComponent.Frame { selfUse = true }, 0.02f);
            check(player.packCount == 2, "Full tank does not consume an oxygen pack");
            oxygen.simulate(10f, false, false);
            player.processInput(new PlayerInputComponent.Frame { selfUse = true }, 0.02f);
            check(player.packCount == 1 && player.oxygenPercent == 100f, "Self-use restores oxygen and consumes exactly one pack");
            player.processInput(new PlayerInputComponent.Frame(), 0.02f);
            check(player.packCount == 1, "Held/neutral input does not repeat item consumption");
            player.processInput(new PlayerInputComponent.Frame { switchHand = true }, 0.02f);
            player.processInput(new PlayerInputComponent.Frame { watch = true, signal = 1 }, 0.02f);
            check(watch.isVisible && watch.lastSignal.Contains("FOLLOW ME"), "Empty hand shows watch and previews a signal");
            player.processInput(new PlayerInputComponent.Frame(), 0.02f);
            check(!watch.isVisible, "Releasing right button lowers the watch");
            player.processInput(new PlayerInputComponent.Frame { switchHand = true, watch = true }, 0.02f);
            check(!watch.isVisible, "Equipped item excludes the watch action");

            warp(player, dummy.transform.position + Vector3.back * 5f);
            player.processInput(new PlayerInputComponent.Frame { otherUse = true }, 0.02f);
            check(player.packCount == 1 && dummy.oxygenPercent == 25f, "Out-of-range recipient does not consume an item");
            warp(player, dummy.transform.position + Vector3.back * 2f);
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube); //벽 너머 사용을 검사할 임시 장애물
            try
            {
                barrier.transform.position = player.transform.position + Vector3.forward;
                barrier.transform.localScale = new Vector3(2f, 3f, 0.2f);
                Physics.SyncTransforms();
                player.processInput(new PlayerInputComponent.Frame { otherUse = true }, 0.02f);
                check(player.packCount == 1 && dummy.oxygenPercent == 25f, "Wall blocks oxygen sharing");
            }
            finally { Object.DestroyImmediate(barrier); }
            Physics.SyncTransforms();
            player.processInput(new PlayerInputComponent.Frame { otherUse = true }, 0.02f);
            check(player.packCount == 0 && Mathf.Abs(dummy.oxygenPercent - 60f) < 0.01f, "Nearby diver receives oxygen through the Player facade");
            check(!player.hasEquippedItem, "Last pack returns the hand to empty");
            check(player.receivePack() && player.receivePack() && player.receivePack() && !player.receivePack(), "Inventory cap rejects extra supplies");

            var pockets = Object.FindObjectsByType<AirPocketComponent>(); //실제 씬 안전 공간
            var entry = pockets.Single(item => item.id == 0); //입구 구역
            var exit = pockets.Single(item => item.id == 1); //목표 구역
            check(entry.contains(new Vector3(0f, 0f, -9f)) && !entry.contains(new Vector3(0f, 0f, 5f)), "Air pocket bounds distinguish water from air");
            oxygen.simulate(30f, false, false);
            var beforeAir = player.oxygenPercent; //공기 구역 입장 전 잔량
            oxygen.simulate(20f, true, false);
            check(Mathf.Abs(player.oxygenPercent - beforeAir) < 0.001f && player.oxygenPercent < 100f, "Air stops consumption without automatic refill");
            var station = Object.FindObjectsByType<DiveInteractableComponent>().First(item => item.label == "REFILL OXYGEN"); //실제 산소 공급 장치
            player.setAirPocket(null);
            check(!station.interact(player), "Refill station requires air-pocket membership");
            player.setAirPocket(entry);
            check(station.interact(player) && player.oxygenPercent == 100f, "Refill is a distinct station interaction");
            check(!station.interact(player), "Refill on a full tank reports no effect");

            testOxygenStates();
            warp(player, new Vector3(0f, 0f, 20f));
            for (var index = 0; index < 180; index++) //벽 충돌까지 수영할 프레임 번호
                player.processInput(new PlayerInputComponent.Frame { movement = Vector2.right }, 1f / 60f);
            check(player.transform.position.x > 1f && player.transform.position.x < 7.5f, "Swimming collides with the cave wall");
            movement.stopMovement();
            warp(player, new Vector3(0f, 0f, 20f));
            var start = player.transform.position; //대각선 이동 시험 시작 위치
            for (var index = 0; index < 30; index++) //대각선 이동 시험 프레임 번호
                player.processInput(new PlayerInputComponent.Frame { movement = Vector2.one, vertical = 1f }, 1f / 60f);
            check(Vector3.Distance(start, player.transform.position) <= 3.2f * 0.5f + 0.03f, "Three-axis input does not exceed swim speed");
            player.setAirPocket(null);
            warp(player, new Vector3(1.1f, 0f, 4f));
            player.processInput(new PlayerInputComponent.Frame { watch = true }, 0.02f);
            for (var index = 0; index < 40; index++) player.simulateVitals(1f / 60f); //워치 자세 표시 안정화
            await Task.Delay(120);
            capture("play-watch");
            var missing = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)); //실제 씬의 누락 스크립트 수
            check(missing == 0, "Play scene has no missing scripts");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3. Prefabs/Prototype/Diver.prefab"); //저장된 플레이어 프리팹
            check(prefab != null && prefab.GetComponent<Player>() != null, "Reusable player prefab is saved");

            player.setAirPocket(null);
            session.enabled = true;
            await Task.Delay(120);
            warp(player, new Vector3(0f, 0f, 60f));
            await Task.Delay(200);
            check(session.state == PrototypeSession.RunState.Complete, "Entering the destination after water completes the route");
            capture("play-complete");
            click("RESTART");
            await waitForScene("Play", player);
            await Task.Delay(200);
            var restarted = Object.FindAnyObjectByType<PrototypeSession>(); //재시작된 세션
            check(restarted.player.oxygenPercent == 100f && restarted.player.packCount == 2 && restarted.state == PrototypeSession.RunState.Exploring,
                "Restart resets oxygen, inventory and run state");
            var result = new { passed = passed.Count, checks = passed.ToArray(), scene = SceneManager.GetActiveScene().name }; //검사 결과
            File.WriteAllText(".utmp/prototype-20260928/validation.json", Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally
        {
            if (session != null) session.enabled = true;
            if (player != null) player.setPaused(true);
        }
    }

    private static void testOxygenStates() //프레임 시간과 구조 상태 경계 검사
    {
        var first = new GameObject("Validation oxygen A"); //검사용 산소 오브젝트
        var second = new GameObject("Validation oxygen B"); //프레임 분할 비교 오브젝트
        try
        {
            var a = first.AddComponent<PlayerOxygenComponent>(); //긴 프레임 검사 대상
            var b = second.AddComponent<PlayerOxygenComponent>(); //짧은 프레임 검사 대상
            a.simulate(10f, false, false);
            for (var index = 0; index < 500; index++) b.simulate(0.02f, false, false); //짧게 나눈 시간 반복
            check(Mathf.Abs(a.oxygenPercent - b.oxygenPercent) < 0.02f, "Oxygen consumption is independent of frame subdivision");
            a.simulate(1000f, false, false);
            check(a.state == PlayerOxygenComponent.LifeState.Downed && a.remainingRescueTime == 20f, "Zero oxygen starts a rescue window");
            check(a.restoreOxygen(35f) && a.state == PlayerOxygenComponent.LifeState.Active, "Oxygen supply rescues a downed diver");
            a.simulate(1000f, false, false);
            a.simulate(21f, false, false);
            check(a.state == PlayerOxygenComponent.LifeState.Eliminated && !a.restoreOxygen(35f), "Expired rescue window needs revival rather than an oxygen pack");
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    private static void warp(Player player, Vector3 position) //테스트 장면 안에서만 플레이어 위치 이동
    {
        var controller = player.GetComponent<CharacterController>(); //위치 이동 중 끌 충돌 캡슐
        controller.enabled = false;
        player.transform.position = position;
        player.transform.rotation = Quaternion.identity;
        player.GetComponent<PlayerMovementComponent>().stopMovement();
        controller.enabled = true;
        Physics.SyncTransforms();
    }

    private static void click(string name) //저장된 실제 UI 버튼의 이벤트 실행
    {
        var button = Object.FindObjectsByType<Button>().Single(item => item.name == name); //검사할 실제 버튼
        button.onClick.Invoke();
    }

    private static async Task waitForScene(string expected, Object oldPlayer = null) //실제 비동기 씬 이동 완료 대기
    {
        var deadline = DateTime.UtcNow.AddSeconds(10); //씬 이동 대기 제한 시각
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
            if (SceneManager.GetActiveScene().name == expected && oldPlayer == null) return;
        }
        throw new InvalidOperationException("Scene transition timed out: " + expected);
    }

    private static void check(bool condition, string description) //실패 시 즉시 중단하고 통과 항목 기록
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + description);
        passed.Add(description);
    }

    public static string capture(string name) //화면 UI를 포함한 동일 카메라 렌더 미리보기 저장
    {
        var camera = Camera.main; //현재 게임 카메라
        var overlays = Object.FindObjectsByType<Canvas>().Where(item => item.renderMode == RenderMode.ScreenSpaceOverlay).ToArray(); //합성할 화면 UI
        var target = new RenderTexture(1280, 720, 24); //검사용 렌더 대상
        var previousTarget = camera.targetTexture; //원래 카메라 출력
        var previousActive = RenderTexture.active; //원래 활성 렌더 대상
        var previousAspect = camera.aspect; //원래 화면 종횡비
        Texture2D image = null; //저장할 미리보기 이미지
        try
        {
            camera.targetTexture = target;
            camera.aspect = 1280f / 720f;
            foreach (var overlay in overlays) //검사용 카메라에 연결할 UI
            {
                overlay.renderMode = RenderMode.ScreenSpaceCamera;
                overlay.worldCamera = camera;
                overlay.planeDistance = 0.12f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
            image.Apply();
            var path = ".utmp/prototype-20260928/" + name + ".png"; //검사용 파일 저장 경로
            File.WriteAllBytes(path, image.EncodeToPNG());
            return path;
        }
        finally
        {
            foreach (var overlay in overlays) //화면 UI의 원래 표시 방식 복원
            {
                overlay.renderMode = RenderMode.ScreenSpaceOverlay;
                overlay.worldCamera = null;
            }
            camera.targetTexture = previousTarget;
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(target);
            if (image != null) Object.DestroyImmediate(image);
            Canvas.ForceUpdateCanvases();
        }
    }

    public static async Task<string> previewWatch() //실제 UI 갱신 프레임 이후 워치 화면 확인
    {
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != "Play") throw new InvalidOperationException("Open Play in Play Mode.");
        var session = Object.FindAnyObjectByType<PrototypeSession>(); //현재 검사할 세션
        var player = session.player; //현재 검사할 플레이어
        session.enabled = false;
        try
        {
            player.setPaused(false);
            warp(player, new Vector3(1.1f, 0f, 4f));
            player.setAirPocket(null);
            player.processInput(new PlayerInputComponent.Frame { watch = true, signal = 1 }, 0.02f);
            for (var index = 0; index < 40; index++) player.simulateVitals(1f / 60f); //손목 자세 안정화
            await Task.Delay(200);
            return capture("play-watch");
        }
        finally
        {
            player.setPaused(true);
            session.enabled = true;
        }
    }
}
