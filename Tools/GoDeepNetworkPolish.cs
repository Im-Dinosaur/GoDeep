using System;
using System.Linq;
using GoDeep;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GoDeepNetworkPolish
{
    public static object apply() //온라인 이동 시험에서 사용할 안내와 로컬 전용 요소 연결
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        for (var index = 0; index < SceneManager.sceneCount; index++) //열린 씬 번호
            if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Save scene changes first.");
        EditorSceneManager.OpenScene("Assets/1. Scenes/Play.unity");
        var session = Object.FindAnyObjectByType<PrototypeSession>(); //온라인과 로컬 모드를 구분할 세션
        var objects = Object.FindObjectsByType<DiveInteractableComponent>().Select(item => item.gameObject)
            .Concat(Object.FindObjectsByType<TextMesh>().Where(item => item.text.StartsWith("O2") || item.text.StartsWith("TRAINING DIVER")).Select(item => item.gameObject)).ToArray(); //온라인에서 숨길 오브젝트 목록
        var serialized = new SerializedObject(session); //세션 연결을 저장할 직렬화 대상
        var property = serialized.FindProperty("offlineOnlyObjects"); //로컬 전용 오브젝트 참조 목록
        property.arraySize = objects.Length;
        for (var index = 0; index < objects.Length; index++) //저장할 오브젝트 번호
            property.GetArrayElementAtIndex(index).objectReferenceValue = objects[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var ui = Object.FindAnyObjectByType<PrototypeUIComponent>(); //기존 플레이 UI
        set(ui, "objectiveText", GameObject.Find("Objective").GetComponent<Text>());
        set(ui, "controlsText", GameObject.Find("Keys").GetComponent<Text>());
        set(ui, "returnButtonText", GameObject.Find("HOME").GetComponentInChildren<Text>());
        set(ui, "restartButton", GameObject.Find("RESTART").GetComponent<Button>());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorSceneManager.OpenScene("Assets/1. Scenes/Home.unity");
        return new { offlineOnlyObjects = objects.Length, ui = "Connected online controls and leave button" };
    }

    private static void set(Object target, string field, Object value) //Inspector 참조 저장
    {
        var serialized = new SerializedObject(target); //편집할 직렬화 대상
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
