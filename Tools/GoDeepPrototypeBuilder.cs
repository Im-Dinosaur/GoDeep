using System;
using System.IO;
using GoDeep;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GoDeepPrototypeBuilder
{
    private const string dataPath = "Assets/6. Data/Prototype"; //시제품 생성 자산 경로
    private static Font font; //시제품 공용 영문 글꼴
    private static Material rock; //동굴 표면 재질
    private static Material suit; //다이버 슈트 재질
    private static Material dark; //장비의 어두운 재질
    private static Material teal; //안내 조명 재질
    private static Material amber; //에어포켓 조명 재질
    private static Mesh caveMesh; //시제품 동굴 메시
    private static readonly Color ink = new Color(0.025f, 0.065f, 0.085f); //UI 기본 배경색
    private static readonly Color mint = new Color(0.35f, 0.93f, 0.79f); //중요한 행동 강조색
    private static readonly Color soft = new Color(0.58f, 0.72f, 0.75f); //보조 안내 색상

    public static object build() //초기 빈 씬에 로컬 조작 시제품 제작
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        for (var index = 0; index < SceneManager.sceneCount; index++) //현재 열려 있는 씬 번호
            if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Save open scene changes before building.");
        if (AssetDatabase.LoadAssetAtPath<Mesh>(dataPath + "/PracticeCave.asset") != null)
            throw new InvalidOperationException("Prototype already exists. Edit the existing scenes instead of overwriting them.");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory("Assets/3. Prefabs/Prototype");
        AssetDatabase.Refresh();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rock = material("CaveRock", new Color(0.11f, 0.2f, 0.23f));
        suit = material("DiverSuit", new Color(0.14f, 0.43f, 0.47f));
        dark = material("Equipment", new Color(0.025f, 0.045f, 0.055f));
        teal = material("GuideLight", new Color(0.1f, 0.66f, 0.62f), true);
        amber = material("AirPocketLight", new Color(1f, 0.57f, 0.19f), true);
        caveMesh = createCaveMesh();
        AssetDatabase.CreateAsset(caveMesh, dataPath + "/PracticeCave.asset");
        createMenu(false);
        createMenu(true);
        createPlay();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/1. Scenes/Home.unity");
        return new { scenes = new[] { "Home", "StandBy", "Play" }, mode = "Local movement prototype", prefab = "Assets/3. Prefabs/Prototype/Diver.prefab" };
    }

    private static Material material(string name, Color color, bool emission = false) //URP 공용 재질 저장
    {
        var result = new Material(Shader.Find("Universal Render Pipeline/Lit")); //생성할 공용 재질
        result.name = name;
        result.SetColor("_BaseColor", color);
        result.SetFloat("_Smoothness", 0.25f);
        if (emission)
        {
            result.EnableKeyword("_EMISSION");
            result.SetColor("_EmissionColor", color * 2f);
        }
        AssetDatabase.CreateAsset(result, dataPath + "/" + name + ".mat");
        return result;
    }

    private static float centerX(float z) //동굴 통로 중심의 완만한 좌우 변화
    {
        return Mathf.Sin((z + 12f) / 88f * Mathf.PI * 2f) * 2.3f;
    }

    private static Mesh createCaveMesh() //안쪽을 향한 면과 충돌을 가진 동굴 메시 생성
    {
        const int rings = 34; //통로를 나누는 단면 수
        const int sides = 16; //단면 둘레의 꼭짓점 수
        var vertices = new Vector3[rings * sides]; //동굴 꼭짓점
        var triangles = new int[(rings - 1) * sides * 6]; //안쪽 면 삼각형
        for (var ring = 0; ring < rings; ring++) //현재 단면 번호
        {
            var z = Mathf.Lerp(-16f, 76f, (float)ring / (rings - 1)); //현재 단면의 진행 좌표
            for (var side = 0; side < sides; side++) //단면 둘레 번호
            {
                var angle = side * Mathf.PI * 2f / sides; //단면의 원주 각도
                var radius = 4.8f + Mathf.Sin(ring * 1.7f + side * 2.3f) * 0.38f; //불규칙 암벽 반경
                vertices[ring * sides + side] = new Vector3(centerX(z) + Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.78f, z);
                if (ring == rings - 1) continue;
                var a = ring * sides + side; //현재 면의 첫 꼭짓점
                var b = a + sides; //다음 단면의 첫 꼭짓점
                var c = ring * sides + (side + 1) % sides; //현재 단면의 다음 꼭짓점
                var d = c + sides; //다음 단면의 다음 꼭짓점
                var offset = (ring * sides + side) * 6; //현재 면의 삼각형 배열 위치
                triangles[offset] = a; triangles[offset + 1] = b; triangles[offset + 2] = c;
                triangles[offset + 3] = c; triangles[offset + 4] = b; triangles[offset + 5] = d;
            }
        }
        var mesh = new Mesh { name = "PracticeCave", vertices = vertices, triangles = triangles }; //저장할 동굴 메시
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static GameObject shape(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material mat,
        Transform parent = null, bool collision = false) //기본 도형으로 장비와 공간 구성
    {
        var result = GameObject.CreatePrimitive(primitive); //새 기본 도형
        result.name = name;
        result.transform.SetParent(parent, false);
        result.transform.localPosition = position;
        result.transform.localScale = scale;
        result.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collision) Object.DestroyImmediate(result.GetComponent<Collider>());
        return result;
    }

    private static void light(string name, Vector3 position, Color color, float intensity, float range) //동굴 조명 배치
    {
        var go = new GameObject(name); //조명 오브젝트
        go.transform.position = position;
        var source = go.AddComponent<Light>(); //점 조명 구성 요소
        source.type = LightType.Point;
        source.color = color;
        source.intensity = intensity;
        source.range = range;
        source.shadows = LightShadows.None;
    }

    private static void environment() //충돌 동굴과 경로와 에어포켓 조명 생성
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.2f, 0.32f, 0.38f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.02f, 0.085f, 0.11f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.027f;
        var cave = new GameObject("Practice Cave"); //안쪽 충돌 동굴
        cave.AddComponent<MeshFilter>().sharedMesh = caveMesh;
        cave.AddComponent<MeshRenderer>().sharedMaterial = rock;
        cave.AddComponent<MeshCollider>().sharedMesh = caveMesh;
        shape("Entry end wall", PrimitiveType.Cube, new Vector3(0f, 0f, -16f), new Vector3(16f, 12f, 1f), rock, collision: true);
        shape("Exit end wall", PrimitiveType.Cube, new Vector3(0f, 0f, 76f), new Vector3(16f, 12f, 1f), rock, collision: true);
        for (var z = -10; z <= 70; z += 5) //경로 표식을 놓을 위치
        {
            var position = new Vector3(centerX(z) - 2.6f, -2.6f, z); //벽 가까이 놓는 안내등
            shape("Route beacon " + z, PrimitiveType.Cube, position, new Vector3(0.16f, 0.16f, 0.8f), teal);
            if (z % 10 == 0) light("Route light " + z, position + Vector3.up, new Color(0.25f, 0.8f, 0.9f), 3f, 9f);
        }
        foreach (var z in new[] { -8f, 65f }) //에어포켓 조명 위치
        {
            shape("Warm pocket lamp", PrimitiveType.Sphere, new Vector3(centerX(z), 2.6f, z), Vector3.one * 0.4f, amber);
            light("Pocket warm light", new Vector3(centerX(z), 1.8f, z), new Color(1f, 0.65f, 0.3f), 10f, 16f);
        }
        worldLabel("EXIT  /  AIR POCKET", new Vector3(centerX(59f), 1.4f, 60f), 0.09f, mint);
    }

    private static Camera camera(string name, Vector3 position) //기본 1인칭 카메라 생성
    {
        var go = new GameObject(name); //카메라 오브젝트
        go.tag = "MainCamera";
        go.transform.position = position;
        var result = go.AddComponent<Camera>(); //시점 카메라
        result.clearFlags = CameraClearFlags.SolidColor;
        result.backgroundColor = ink;
        result.fieldOfView = 75f;
        result.nearClipPlane = 0.03f;
        result.farClipPlane = 160f;
        go.AddComponent<AudioListener>();
        return result;
    }

    private static RectTransform rect(Transform parent, string name, Vector2 position, Vector2 size) //좌상단 기준 UI 영역 생성
    {
        var go = new GameObject(name, typeof(RectTransform)); //UI 오브젝트
        var result = go.GetComponent<RectTransform>(); //UI 위치와 크기
        result.SetParent(parent, false);
        result.anchorMin = result.anchorMax = result.pivot = new Vector2(0f, 1f);
        result.anchoredPosition = new Vector2(position.x, -position.y);
        result.sizeDelta = size;
        return result;
    }

    private static Image panel(Transform parent, string name, Vector2 position, Vector2 size, Color color) //단색 UI 배경 생성
    {
        var result = rect(parent, name, position, size).gameObject.AddComponent<Image>(); //UI 배경 이미지
        result.color = color;
        result.raycastTarget = false;
        return result;
    }

    private static Text label(Transform parent, string name, string content, int size, Vector2 position, Vector2 box, Color color) //공용 UI 텍스트 생성
    {
        var result = rect(parent, name, position, box).gameObject.AddComponent<Text>(); //표시할 UI 텍스트
        result.font = font;
        result.text = content;
        result.fontSize = size;
        result.color = color;
        result.horizontalOverflow = HorizontalWrapMode.Wrap;
        result.verticalOverflow = VerticalWrapMode.Truncate;
        result.raycastTarget = false;
        return result;
    }

    private static Button button(Transform parent, string title, Vector2 position, Vector2 size, UnityAction action, bool primary = true) //영구 클릭 연결이 있는 버튼 생성
    {
        var image = panel(parent, title, position, size, primary ? mint : new Color(0.08f, 0.17f, 0.2f)); //버튼 바탕
        image.raycastTarget = true;
        var result = image.gameObject.AddComponent<Button>(); //클릭 처리 버튼
        result.targetGraphic = image;
        var colors = result.colors; //버튼 상태별 색상
        colors.highlightedColor = new Color(0.75f, 1f, 0.94f);
        colors.pressedColor = new Color(0.5f, 0.78f, 0.74f);
        result.colors = colors;
        var text = label(image.transform, "Label", title, 19, Vector2.zero, size, primary ? ink : Color.white); //버튼 제목
        text.alignment = TextAnchor.MiddleCenter;
        UnityEventTools.AddPersistentListener(result.onClick, action);
        return result;
    }

    private static Canvas canvas() //화면 크기에 맞추는 UI와 새 입력 시스템 연결
    {
        var go = new GameObject("Prototype Interface", typeof(RectTransform)); //최상위 UI
        var result = go.AddComponent<Canvas>(); //화면 UI 캔버스
        result.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>(); //해상도 대응 설정
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        var events = new GameObject("EventSystem"); //UI 입력 전달 오브젝트
        events.AddComponent<EventSystem>();
        events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        return result;
    }

    private static void createMenu(bool standby) //홈과 로컬 준비 화면의 실제 씬 저장
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        environment();
        camera("Menu Camera", new Vector3(-0.2f, 0.2f, -7f));
        var ui = canvas(); //메뉴 캔버스
        var group = ui.gameObject.AddComponent<CanvasGroup>(); //메뉴 입력 그룹
        var flow = new GameObject("GameFlow").AddComponent<GameFlow>(); //씬 이동 진입점
        set(flow, "menuGroup", group);
        panel(ui.transform, "Left shade", Vector2.zero, new Vector2(670f, 720f), new Color(0.015f, 0.045f, 0.06f, 0.94f));
        panel(ui.transform, "Accent", new Vector2(64f, 66f), new Vector2(42f, 4f), mint);
        label(ui.transform, "Edition", standby ? "01  /  DIVE PREPARATION" : "00  /  FIELD PROTOTYPE", 16,
            new Vector2(64f, 91f), new Vector2(560f, 30f), mint);
        label(ui.transform, "Title", standby ? "BEFORE\nTHE DESCENT" : "GO\nDEEP", standby ? 62 : 96,
            new Vector2(60f, 153f), new Vector2(580f, 224f), Color.white);
        label(ui.transform, "Subtitle", standby ? "One short cave. Learn the equipment." : "Stay close. Share your air. Find a way through.", 21,
            new Vector2(64f, 389f), new Vector2(540f, 54f), Color.white);
        label(ui.transform, "Description", standby
            ? "Watch on your left wrist. Oxygen packs in your kit.\nFollow the cyan markers to the next warm air pocket.\nA training diver is waiting inside the cave."
            : "A first-person underwater co-op exploration game.\nThis build tests swimming, the watch and oxygen use.\nMultiplayer and voice will follow in the next stages.",
            18, new Vector2(64f, 457f), new Vector2(530f, 90f), soft);
        if (standby)
        {
            button(ui.transform, "START LOCAL PRACTICE", new Vector2(64f, 584f), new Vector2(338f, 56f), flow.startPrototype);
            button(ui.transform, "BACK", new Vector2(420f, 584f), new Vector2(174f, 56f), flow.returnHome, false);
        }
        else button(ui.transform, "PREPARE TO DIVE  >", new Vector2(64f, 584f), new Vector2(338f, 56f), flow.openStandBy);
        label(ui.transform, "Footer", "M1  /  LOCAL PRACTICE     -     WINDOWS + KEYBOARD / MOUSE", 12,
            new Vector2(64f, 674f), new Vector2(680f, 24f), soft);
        panel(ui.transform, "Field card", new Vector2(876f, 490f), new Vector2(340f, 168f), new Color(0.02f, 0.07f, 0.085f, 0.93f));
        label(ui.transform, "Field card title", standby ? "YOUR LOADOUT" : "EXPEDITION 001", 17,
            new Vector2(900f, 514f), new Vector2(300f, 35f), mint);
        label(ui.transform, "Field card detail", standby ? "O2  100%\n2 oxygen packs  /  Dive watch\nMode: local practice" : "BLUE HOLE / FLOODED CAVE\nObjective: next air pocket\nRoute markers: cyan", 17,
            new Vector2(900f, 556f), new Vector2(296f, 94f), Color.white);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/1. Scenes/" + (standby ? "StandBy" : "Home") + ".unity");
    }

    private static void worldLabel(string content, Vector3 position, float scale, Color color) //동굴 안에서 읽을 수 있는 임시 표지
    {
        var go = new GameObject(content); //표지 오브젝트
        go.transform.position = position;
        var text = go.AddComponent<TextMesh>(); //공간 표시 문자
        text.text = content;
        text.font = font;
        text.fontSize = 48;
        text.characterSize = scale;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    private static AirPocketComponent pocket(string name, Vector3 position, Vector3 size, int id, bool destination) //안전 공간 판정 구역 생성
    {
        var go = new GameObject(name); //에어포켓 구역 오브젝트
        go.transform.position = position;
        var bounds = go.AddComponent<BoxCollider>(); //공간 범위
        bounds.isTrigger = true;
        bounds.size = size;
        var result = go.AddComponent<AirPocketComponent>(); //에어포켓 기능
        set(result, "pocketId", id);
        set(result, "pocketName", name);
        set(result, "isDestination", destination);
        return result;
    }

    private static void supply(Vector3 position, bool station) //산소팩 또는 산소 공급 장치 배치
    {
        var go = shape(station ? "Oxygen Refill Station" : "Oxygen Supply Pack", PrimitiveType.Cylinder,
            position, station ? new Vector3(0.7f, 0.6f, 0.7f) : new Vector3(0.3f, 0.25f, 0.3f), station ? amber : teal, collision: true); //상호작용 장비
        var interaction = go.AddComponent<DiveInteractableComponent>(); //장치 상호작용 기능
        set(interaction, "interactionKind", station ? 1 : 0);
        set(interaction, "interactionLabel", station ? "REFILL OXYGEN" : "COLLECT OXYGEN PACK");
        worldLabel(station ? "O2 REFILL\n[E]" : "O2 PACK", position + Vector3.up * (station ? 1f : 0.6f), 0.055f, mint);
    }

    private static Player createDiver(out Text wristReadout) //로컬 수영 플레이어와 임시 손 장비 구성
    {
        var go = new GameObject("Diver"); //플레이어 진입점 오브젝트
        go.layer = 2;
        go.transform.position = new Vector3(0f, 0f, -9f);
        var controller = go.AddComponent<CharacterController>(); //수영 충돌 캡슐
        controller.radius = 0.32f;
        controller.height = 1.7f;
        controller.center = new Vector3(0f, -0.45f, 0f);
        controller.stepOffset = 0f;
        controller.skinWidth = 0.04f;
        var view = camera("Diver Camera", go.transform.position); //로컬 다이버 카메라
        view.transform.SetParent(go.transform, true);
        var lamp = new GameObject("Dive Lamp").AddComponent<Light>(); //다이버가 비추는 조명
        lamp.transform.SetParent(view.transform, false);
        lamp.type = LightType.Spot;
        lamp.color = new Color(0.72f, 0.92f, 1f);
        lamp.intensity = 14f;
        lamp.range = 24f;
        lamp.spotAngle = 82f;
        lamp.innerSpotAngle = 50f;
        lamp.shadows = LightShadows.Soft;
        var left = new GameObject("Left Arm - Watch").transform; //왼팔 자세 기준
        left.SetParent(view.transform, false);
        left.localPosition = new Vector3(-0.34f, -0.4f, 0.42f);
        var right = new GameObject("Right Arm - Item").transform; //오른팔 자세 기준
        right.SetParent(view.transform, false);
        right.localPosition = new Vector3(0.34f, -0.4f, 0.45f);
        foreach (var arm in new[] { left, right }) //임시 전완과 장갑을 만들 팔
        {
            shape("Sleeve", PrimitiveType.Cube, new Vector3(0f, -0.075f, 0.25f), new Vector3(0.14f, 0.15f, 0.4f), suit, arm);
            shape("Glove", PrimitiveType.Cube, new Vector3(0f, -0.04f, 0.08f), new Vector3(0.15f, 0.14f, 0.15f), dark, arm);
        }
        shape("Watch Case", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.14f), new Vector3(0.25f, 0.17f, 0.065f), dark, left);
        var watchCanvas = new GameObject("Watch Screen", typeof(RectTransform)).AddComponent<Canvas>(); //손목에 붙는 실제 화면
        watchCanvas.renderMode = RenderMode.WorldSpace;
        watchCanvas.transform.SetParent(left, false);
        watchCanvas.transform.localPosition = new Vector3(-0.112f, 0.093f, 0.105f);
        watchCanvas.transform.localScale = Vector3.one * 0.0009f;
        var watchRect = watchCanvas.GetComponent<RectTransform>(); //손목 화면 영역
        watchRect.pivot = new Vector2(0f, 1f);
        watchRect.sizeDelta = new Vector2(240f, 160f);
        wristReadout = label(watchRect, "Oxygen and Depth", "O2  100%\n08.0 m", 34, new Vector2(8f, 8f), new Vector2(230f, 142f), mint);
        var held = shape("Held Oxygen Pack", PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0.08f), new Vector3(0.12f, 0.15f, 0.12f), amber, right); //손에 든 임시 산소팩
        held.SetActive(false);
        var input = go.AddComponent<PlayerInputComponent>(); //입력 구성 요소
        var movement = go.AddComponent<PlayerMovementComponent>(); //수영 구성 요소
        set(movement, "controller", controller);
        set(movement, "viewPivot", view.transform);
        var oxygen = go.AddComponent<PlayerOxygenComponent>(); //산소 구성 요소
        var items = go.AddComponent<PlayerItemComponent>(); //소지품 구성 요소
        var watch = go.AddComponent<PlayerWatchComponent>(); //워치 구성 요소
        set(watch, "wristReadout", wristReadout);
        var presentation = go.AddComponent<PlayerPresentationComponent>(); //손 동작 구성 요소
        var audio = go.AddComponent<AudioSource>(); //시제품 피드백 소리
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        set(presentation, "leftArm", left);
        set(presentation, "rightArm", right);
        set(presentation, "heldPack", held);
        set(presentation, "feedbackAudio", audio);
        var player = go.AddComponent<Player>(); //외부에서 사용할 플레이어 파사드
        set(player, "viewCamera", view);
        set(player, "inputComponent", input);
        set(player, "movementComponent", movement);
        set(player, "oxygenComponent", oxygen);
        set(player, "itemComponent", items);
        set(player, "watchComponent", watch);
        set(player, "presentationComponent", presentation);
        return player;
    }

    private static Player createTrainingDiver() //산소 공유 대상이 되는 움직이지 않는 연습 다이버 제작
    {
        var root = new GameObject("Training Diver"); //연습 다이버 진입점
        root.transform.position = new Vector3(centerX(12f) + 1f, 0f, 12f);
        var hitbox = root.AddComponent<CapsuleCollider>(); //산소 사용 조준 영역
        hitbox.center = new Vector3(0f, -0.3f, 0f);
        hitbox.height = 1.8f;
        hitbox.radius = 0.48f;
        shape("Suit", PrimitiveType.Capsule, new Vector3(0f, -0.5f, 0f), new Vector3(0.65f, 0.65f, 0.4f), suit, root.transform);
        shape("Helmet", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0f), new Vector3(0.65f, 0.6f, 0.6f), amber, root.transform);
        shape("Visor", PrimitiveType.Cube, new Vector3(0f, 0.3f, -0.28f), new Vector3(0.48f, 0.23f, 0.1f), dark, root.transform);
        shape("Tank", PrimitiveType.Cylinder, new Vector3(0f, -0.4f, 0.35f), new Vector3(0.35f, 0.55f, 0.35f), amber, root.transform);
        var oxygen = root.AddComponent<PlayerOxygenComponent>(); //연습 대상 산소 상태
        set(oxygen, "startingOxygenPercent", 25f);
        var player = root.AddComponent<Player>(); //동료 상호작용 파사드
        set(player, "diverName", "TRAINING DIVER");
        set(player, "locallyControlled", false);
        set(player, "oxygenComponent", oxygen);
        worldLabel("TRAINING DIVER\n[Q] EQUIP  /  [RMB] SHARE", root.transform.position + Vector3.up * 1.2f, 0.05f, mint);
        return player;
    }

    private static void createPlay() //플레이 씬에 시제품 시스템과 UI 연결
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        environment();
        var entry = pocket("ENTRY POCKET", new Vector3(0f, 0f, -8f), new Vector3(14f, 12f, 14f), 0, false); //출발 에어포켓
        var exit = pocket("EXIT POCKET", new Vector3(0f, 0f, 66f), new Vector3(14f, 12f, 16f), 1, true); //도착 에어포켓
        supply(new Vector3(-1.8f, -0.5f, -5f), true);
        supply(new Vector3(centerX(65f) - 1.8f, -0.5f, 65f), true);
        supply(new Vector3(centerX(28f) - 1.6f, -0.4f, 28f), false);
        supply(new Vector3(centerX(43f) + 1.8f, -0.4f, 43f), false);
        var player = createDiver(out var wrist); //로컬 다이버와 손목 화면
        var dummy = createTrainingDiver(); //산소 공유 연습 대상
        var flow = new GameObject("GameFlow").AddComponent<GameFlow>(); //씬 이동 진입점
        var session = new GameObject("PrototypeSession").AddComponent<PrototypeSession>(); //연습 진행 진입점
        set(session, "localPlayer", player);
        set(session, "trainingDiver", dummy);
        set(session, "gameFlow", flow);
        set(session, "airPockets", new Object[] { entry, exit });
        var ui = canvas(); //플레이 UI 캔버스
        var bindings = ui.gameObject.AddComponent<PrototypeUIComponent>(); //플레이 UI 갱신 담당
        set(bindings, "session", session);
        label(ui.transform, "Brand", "GO DEEP", 26, new Vector2(38f, 28f), new Vector2(300f, 38f), Color.white);
        label(ui.transform, "Objective", "001  /  REACH THE NEXT AIR POCKET", 14, new Vector2(40f, 73f), new Vector2(580f, 28f), mint);
        set(bindings, "locationText", label(ui.transform, "Location", "", 17, new Vector2(740f, 35f), new Vector2(500f, 40f), mint));
        set(bindings, "elapsedText", label(ui.transform, "Elapsed", "", 14, new Vector2(1040f, 74f), new Vector2(190f, 30f), soft));
        panel(ui.transform, "Progress track", new Vector2(40f, 108f), new Vector2(360f, 3f), new Color(0.2f, 0.3f, 0.33f));
        var progress = panel(ui.transform, "Progress", new Vector2(40f, 108f), new Vector2(360f, 3f), mint); //목적지 방향 진행 안내
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        set(bindings, "progressFill", progress);
        var crosshair = label(ui.transform, "Crosshair", "+", 22, new Vector2(626f, 346f), new Vector2(28f, 30f), new Color(0.8f, 1f, 0.95f, 0.7f)); //화면 중앙의 조준점
        crosshair.alignment = TextAnchor.MiddleCenter;
        var interaction = label(ui.transform, "Interaction", "", 17, new Vector2(290f, 538f), new Vector2(700f, 72f), Color.white); //조준 행동 안내
        interaction.alignment = TextAnchor.MiddleCenter;
        set(bindings, "interactionText", interaction);
        set(bindings, "notificationText", label(ui.transform, "Notification", "", 18, new Vector2(400f, 155f), new Vector2(640f, 60f), mint));
        set(bindings, "warningText", label(ui.transform, "Warning", "", 22, new Vector2(380f, 234f), new Vector2(600f, 100f), new Color(1f, 0.58f, 0.25f)));
        set(bindings, "inventoryText", label(ui.transform, "Inventory", "", 17, new Vector2(970f, 632f), new Vector2(285f, 74f), Color.white));
        label(ui.transform, "Keys", "WASD  SWIM    SPACE / CTRL  UP / DOWN\nSHIFT  FASTER    E  INTERACT    ESC  PAUSE", 13,
            new Vector2(40f, 646f), new Vector2(650f, 62f), soft);
        var watchPanel = panel(ui.transform, "Watch details", new Vector2(42f, 352f), new Vector2(366f, 205f), new Color(0.015f, 0.07f, 0.08f, 0.95f)); //워치 상세 정보 영역
        var watchGroup = watchPanel.gameObject.AddComponent<CanvasGroup>(); //워치 상세 표시 그룹
        watchGroup.alpha = 0f;
        watchGroup.blocksRaycasts = false;
        label(watchPanel.transform, "Heading", "DIVE COMPUTER  /  LOCAL SIGNALS", 15, new Vector2(18f, 18f), new Vector2(330f, 24f), mint);
        var details = label(watchPanel.transform, "Details", "", 16, new Vector2(18f, 53f), new Vector2(330f, 145f), Color.white); //상태와 신호 상세 표시
        set(player.GetComponent<PlayerWatchComponent>(), "detailsPanel", watchGroup);
        set(player.GetComponent<PlayerWatchComponent>(), "detailsReadout", details);
        var pause = panel(ui.transform, "Pause", Vector2.zero, new Vector2(1280f, 720f), new Color(0.01f, 0.035f, 0.05f, 0.96f)); //시작과 결과 안내 배경
        pause.raycastTarget = true;
        var pauseGroup = pause.gameObject.AddComponent<CanvasGroup>(); //일시정지 화면 입력 그룹
        set(bindings, "pausePanel", pauseGroup);
        label(pause.transform, "Tag", "GO DEEP  /  MOVEMENT LAB", 18, new Vector2(160f, 100f), new Vector2(860f, 30f), mint);
        set(bindings, "pauseTitle", label(pause.transform, "Title", "READY TO GO DEEP?", 48, new Vector2(156f, 162f), new Vector2(980f, 90f), Color.white));
        set(bindings, "pauseDescription", label(pause.transform, "Description", "", 18, new Vector2(160f, 267f), new Vector2(1000f, 258f), soft));
        set(bindings, "resumeButton", button(pause.transform, "DIVE / RESUME", new Vector2(160f, 571f), new Vector2(282f, 56f), session.resumeDive));
        button(pause.transform, "RESTART", new Vector2(462f, 571f), new Vector2(230f, 56f), flow.restartPrototype, false);
        button(pause.transform, "HOME", new Vector2(712f, 571f), new Vector2(230f, 56f), flow.returnHome, false);
        //프리팹에는 씬 UI 참조를 저장하지 않고 실제 씬 인스턴스에만 연결한다.
        var watchComponent = player.GetComponent<PlayerWatchComponent>(); //씬 UI 연결을 분리할 워치
        set(watchComponent, "detailsPanel", null);
        set(watchComponent, "detailsReadout", null);
        PrefabUtility.SaveAsPrefabAssetAndConnect(player.gameObject, "Assets/3. Prefabs/Prototype/Diver.prefab", InteractionMode.AutomatedAction);
        set(watchComponent, "detailsPanel", watchGroup);
        set(watchComponent, "detailsReadout", details);
        PrefabUtility.RecordPrefabInstancePropertyModifications(watchComponent);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/1. Scenes/Play.unity");
    }

    private static void set(Object target, string field, object value) //Inspector 직렬화 값을 안전하게 연결
    {
        var serialized = new SerializedObject(target); //대상 직렬화 정보
        var property = serialized.FindProperty(field); //수정할 Inspector 필드
        if (property == null) throw new ArgumentException(target.name + " has no field " + field);
        if (value is Object[] array)
        {
            property.arraySize = array.Length;
            for (var index = 0; index < array.Length; index++) //연결할 배열 원소 번호
                property.GetArrayElementAtIndex(index).objectReferenceValue = array[index];
        }
        else if (value == null || value is Object) property.objectReferenceValue = value as Object;
        else if (value is bool boolean) property.boolValue = boolean;
        else if (value is int integer) property.intValue = integer;
        else if (value is float number) property.floatValue = number;
        else if (value is string text) property.stringValue = text;
        else throw new ArgumentException("Unsupported field value for " + field);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
