using System;
using GoDeep;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GoDeepPrototypePolish
{
    public static object apply() //검증에서 확인한 손목 가림과 UI 가독성 조정
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
        var suit = viewMaterial("SleeveView", new Color(0.10f, 0.31f, 0.34f)); //1인칭 소매의 일정한 색상
        var gear = viewMaterial("EquipmentView", new Color(0.018f, 0.028f, 0.035f)); //워치와 장갑의 일정한 색상
        var prefabPath = "Assets/3. Prefabs/Prototype/Diver.prefab"; //수정할 다이버 프리팹
        var root = PrefabUtility.LoadPrefabContents(prefabPath); //분리해 편집할 프리팹
        try
        {
            foreach (var name in new[] { "Left Arm - Watch", "Right Arm - Item" }) //조정할 양팔 이름
            {
                var arm = root.transform.Find("Diver Camera/" + name); //현재 조정할 팔
                var sleeve = arm.Find("Sleeve"); //손목 뒤로 옮길 소매
                sleeve.localPosition = new Vector3(0f, -0.075f, 0.34f);
                sleeve.localScale = new Vector3(0.12f, 0.13f, 0.34f);
                sleeve.GetComponent<Renderer>().sharedMaterial = suit;
                var glove = arm.Find("Glove"); //워치를 가리지 않게 옮길 장갑
                glove.localPosition = new Vector3(0f, -0.065f, 0.57f);
                glove.localScale = new Vector3(0.13f, 0.12f, 0.14f);
                glove.GetComponent<Renderer>().sharedMaterial = gear;
            }
            var left = root.transform.Find("Diver Camera/Left Arm - Watch"); //왼손 워치 기준
            var watchCase = left.Find("Watch Case"); //읽기 쉬운 크기의 시계 외형
            watchCase.localPosition = new Vector3(0f, 0.01f, 0.125f);
            watchCase.localScale = new Vector3(0.225f, 0.165f, 0.065f);
            watchCase.GetComponent<Renderer>().sharedMaterial = gear;
            var watchScreen = left.Find("Watch Screen"); //워치 전면 화면
            watchScreen.localPosition = new Vector3(-0.106f, 0.082f, 0.091f);
            watchScreen.localScale = Vector3.one * 0.00085f;
            watchScreen.GetComponentInChildren<Text>().fontSize = 30;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        EditorSceneManager.OpenScene("Assets/1. Scenes/Play.unity");
        var ui = GameObject.Find("Prototype Interface").transform; //플레이 안내 화면
        var pause = ui.Find("Pause").GetComponent<Image>(); //다른 글자가 비치지 않게 할 메뉴 배경
        pause.color = new Color(0.01f, 0.035f, 0.05f, 1f);
        var watchPanel = ui.Find("Watch details").GetComponent<RectTransform>(); //손목을 가리지 않게 할 상세 패널
        watchPanel.anchoredPosition = new Vector2(36f, -336f);
        watchPanel.sizeDelta = new Vector2(350f, 190f);
        watchPanel.Find("Heading").GetComponent<Text>().fontSize = 14;
        watchPanel.Find("Details").GetComponent<Text>().fontSize = 15;
        ui.Find("Progress").GetComponent<Image>().type = Image.Type.Simple;
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/1. Scenes/Home.unity");
        return "Updated wrist visibility, menu opacity and progress presentation.";
    }

    private static Material viewMaterial(string name, Color color) //조명의 영향을 받지 않는 임시 손 재질 저장
    {
        var path = "Assets/6. Data/Prototype/" + name + ".mat"; //재질 자산 경로
        var result = AssetDatabase.LoadAssetAtPath<Material>(path); //기존 재질 확인
        if (result != null) return result;
        result = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        result.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(result, path);
        return result;
    }
}
