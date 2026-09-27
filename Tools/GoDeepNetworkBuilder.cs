using System;
using System.IO;
using Fusion;
using GoDeep;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GoDeepNetworkBuilder
{
    private const string prefabPath = "Assets/3. Prefabs/Prototype/NetworkDiver.prefab"; //온라인 다이버 저장 경로
    private static readonly Color mint = new Color(0.35f, 0.93f, 0.79f); //중요 행동 강조색
    private static readonly Color soft = new Color(0.58f, 0.72f, 0.75f); //보조 안내색

    public static object build() //기존 시제품을 유지하면서 온라인 프리팹과 메뉴 추가
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        for (var index = 0; index < SceneManager.sceneCount; index++) //열린 씬 번호
            if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Save scene changes first.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            throw new InvalidOperationException("Online assets already exist. Edit them without rebuilding.");
        var backup = ".utmp/network-20260928/before"; //온라인 변경 전 씬 백업 위치
        Directory.CreateDirectory(backup);
        foreach (var name in new[] { "Home", "StandBy", "Play" }) //원본을 보존할 씬 이름
            File.Copy("Assets/1. Scenes/" + name + ".unity", backup + "/" + name + ".unity", false);
        createDiver();
        createMenu();
        EditorSceneManager.OpenScene("Assets/1. Scenes/Home.unity");
        GameObject.Find("Description").GetComponent<Text>().text = "A first-person underwater co-op exploration game.\nPractice the equipment solo or swim with up to 4 divers.\nOnline movement is ready to test. Voice comes next.";
        GameObject.Find("Footer").GetComponent<Text>().text = "M2  /  ONLINE MOVEMENT     -     WINDOWS + KEYBOARD / MOUSE";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return new { prefab = prefabPath, scenes = new[] { "Home", "StandBy" }, maximumPlayers = 4 };
    }

    private static void createDiver() //로컬 프리팹을 복제하여 네트워크 전신과 권한 구성
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/3. Prefabs/Prototype/Diver.prefab"); //원본과 독립적으로 편집할 다이버
        try
        {
            root.name = "NetworkDiver";
            root.transform.position = Vector3.zero;
            var networkObject = root.AddComponent<NetworkObject>(); //네트워크 식별과 수명 설정
            networkObject.Flags |= NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
            networkObject.Flags &= ~NetworkObjectFlags.AllowStateAuthorityOverride;
            root.AddComponent<NetworkTransform>();
            var networkDiver = root.AddComponent<NetworkDiverComponent>(); //자기 입력과 원격 표현 연결
            var body = new GameObject("Remote Body").transform; //원격 참가자가 보는 임시 전신 기준
            body.SetParent(root.transform, false);
            var suit = AssetDatabase.LoadAssetAtPath<Material>("Assets/6. Data/Prototype/DiverSuit.mat"); //기존 공용 슈트 재질
            var dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/6. Data/Prototype/Equipment.mat"); //기존 장비 재질
            var bright = AssetDatabase.LoadAssetAtPath<Material>("Assets/6. Data/Prototype/AirPocketLight.mat"); //동료 식별용 밝은 재질
            shape(body, "Torso", PrimitiveType.Capsule, new Vector3(0f, -0.45f, 0f), new Vector3(0.56f, 0.42f, 0.4f), suit);
            var head = new GameObject("Head").transform; //상하 시선 회전 기준
            head.SetParent(body, false);
            head.localPosition = new Vector3(0f, 0.1f, 0f);
            shape(head, "Hood", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.44f, suit);
            shape(head, "Mask", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0.18f), new Vector3(0.35f, 0.15f, 0.12f), bright);
            shape(body, "Tank", PrimitiveType.Cylinder, new Vector3(0f, -0.38f, -0.32f), new Vector3(0.25f, 0.37f, 0.25f), bright);
            foreach (var side in new[] { -1f, 1f }) //전신 좌우 장비 위치
            {
                shape(body, "Arm", PrimitiveType.Capsule, new Vector3(side * 0.36f, -0.38f, 0.1f), new Vector3(0.17f, 0.33f, 0.17f), suit);
                shape(body, "Leg", PrimitiveType.Capsule, new Vector3(side * 0.15f, -1.04f, 0f), new Vector3(0.2f, 0.32f, 0.2f), suit);
                shape(body, "Fin", PrimitiveType.Cube, new Vector3(side * 0.15f, -1.32f, 0.2f), new Vector3(0.24f, 0.07f, 0.48f), dark);
            }
            set(networkDiver, "player", root.GetComponent<Player>());
            set(networkDiver, "remoteBody", body.gameObject);
            set(networkDiver, "remoteHead", head);
            root.GetComponentInChildren<Camera>().gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void createMenu() //준비 씬의 오른쪽에 방 생성과 입장 카드 연결
    {
        EditorSceneManager.OpenScene("Assets/1. Scenes/StandBy.unity");
        var canvas = Object.FindAnyObjectByType<Canvas>(); //기존 메뉴 캔버스
        foreach (var name in new[] { "Field card", "Field card title", "Field card detail" }) //온라인 카드로 대체할 안내 요소
            Object.DestroyImmediate(GameObject.Find(name));
        var root = new GameObject("NetworkSession"); //씬을 넘어 유지할 연결 루트
        var connection = root.AddComponent<FusionConnectionComponent>(); //SDK 연결 구성 요소
        var session = root.AddComponent<NetworkSession>(); //메뉴에서 사용할 네트워크 파사드
        set(connection, "diverPrefab", AssetDatabase.LoadAssetAtPath<NetworkObject>(prefabPath));
        set(session, "connectionComponent", connection);
        var menu = canvas.gameObject.AddComponent<NetworkMenuComponent>(); //준비 화면의 연결 입력
        panel(canvas.transform, "Online card", new Vector2(724f, 116f), new Vector2(492f, 526f));
        label(canvas.transform, "Online heading", "DIVE TOGETHER", new Vector2(752f, 143f), new Vector2(440f, 40f), 27, mint);
        label(canvas.transform, "Online details", "Up to 4 divers / shared movement\nOxygen, items and voice come next.", new Vector2(752f, 193f), new Vector2(440f, 65f), 18, Color.white);
        var create = button(canvas.transform, "CREATE ROOM", new Vector2(752f, 283f), new Vector2(436f, 52f), menu.createRoom); //새 방 생성 버튼
        label(canvas.transform, "Code label", "HAVE A ROOM CODE?", new Vector2(752f, 364f), new Vector2(436f, 26f), 14, soft);
        var inputRect = rect(canvas.transform, "Room code", new Vector2(752f, 403f), new Vector2(244f, 52f)); //코드 입력 영역
        var image = inputRect.gameObject.AddComponent<Image>(); //입력 배경
        image.color = new Color(0.08f, 0.17f, 0.2f);
        var input = inputRect.gameObject.AddComponent<InputField>(); //영문과 숫자 코드 입력
        input.targetGraphic = image;
        input.textComponent = label(inputRect, "Value", "", new Vector2(14f, 8f), new Vector2(216f, 38f), 23, Color.white);
        input.placeholder = label(inputRect, "Placeholder", "8-CHAR CODE", new Vector2(14f, 11f), new Vector2(216f, 38f), 17, soft);
        input.contentType = InputField.ContentType.Alphanumeric;
        input.characterLimit = 8;
        var join = button(canvas.transform, "JOIN ROOM", new Vector2(1012f, 403f), new Vector2(176f, 52f), menu.joinRoom); //코드 입장 버튼
        var status = label(canvas.transform, "Connection status", "Preparing connection...", new Vector2(752f, 493f), new Vector2(436f, 122f), 17, soft); //연결 결과 표시
        set(menu, "roomCodeInput", input);
        set(menu, "statusText", status);
        set(menu, "createButton", create);
        set(menu, "joinButton", join);
        set(menu, "localButton", GameObject.Find("START LOCAL PRACTICE").GetComponent<Button>());
        set(menu, "backButton", GameObject.Find("BACK").GetComponent<Button>());
        GameObject.Find("Footer").GetComponent<Text>().text = "M2  /  LOCAL PRACTICE + ONLINE MOVEMENT     -     WINDOWS";
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    private static RectTransform rect(Transform parent, string name, Vector2 position, Vector2 size) //좌상단 기준 UI 배치
    {
        var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); //새 UI 영역
        result.SetParent(parent, false);
        result.anchorMin = result.anchorMax = result.pivot = new Vector2(0f, 1f);
        result.anchoredPosition = new Vector2(position.x, -position.y);
        result.sizeDelta = size;
        return result;
    }

    private static Text label(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, Color color) //기본 영문 글꼴 안내 생성
    {
        var text = rect(parent, name, position, size).gameObject.AddComponent<Text>(); //새 안내 문자
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = color;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    private static void panel(Transform parent, string name, Vector2 position, Vector2 size) //반투명 카드 배경 생성
    {
        rect(parent, name, position, size).gameObject.AddComponent<Image>().color = new Color(0.02f, 0.07f, 0.085f, 0.96f);
    }

    private static Button button(Transform parent, string name, Vector2 position, Vector2 size, UnityAction action) //저장되는 버튼 이벤트 연결
    {
        var area = rect(parent, name, position, size); //버튼 영역
        var background = area.gameObject.AddComponent<Image>(); //버튼 배경
        background.color = mint;
        var result = area.gameObject.AddComponent<Button>(); //실제 클릭 대상
        result.targetGraphic = background;
        UnityEventTools.AddPersistentListener(result.onClick, action);
        var text = label(area, "Label", name, Vector2.zero, size, 17, new Color(0.02f, 0.07f, 0.085f)); //버튼 제목
        text.alignment = TextAnchor.MiddleCenter;
        return result;
    }

    private static void shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material) //충돌 없는 임시 다이버 도형 생성
    {
        var shape = GameObject.CreatePrimitive(type); //전신 도형
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        shape.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(shape.GetComponent<Collider>());
    }

    private static void set(Object target, string field, Object value) //Inspector 참조 직렬화
    {
        var serialized = new SerializedObject(target); //편집할 직렬화 대상
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
