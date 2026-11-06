// Naija Streets (Unity rebuild) — HUDSceneBuilder (Editor only, NOT part of the game).
// Builds the two HUD test scenes WITHOUT touching Greybox_M1.unity:
//   Assets/Scenes/HUDTest.unity   — UIDocument + NLHudController + a small
//                                   greybox street so the radar has a scene.
//   Assets/Scenes/IconSheet.unity — every icon in Assets/UI/Icons on a grid
//                                   (generated UXML, for the visual check).
// Entry: -executeMethod HUDSceneBuilder.BuildAll (or the Naija Streets menu).

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using NaijaLife.UI;

public static class HUDSceneBuilder
{
    private const string HudTestPath = "Assets/Scenes/HUDTest.unity";
    private const string IconSheetPath = "Assets/Scenes/IconSheet.unity";
    private const string PanelSettingsPath = "Assets/UI/NLHudPanelSettings.asset";
    private const string IconSheetUxmlPath = "Assets/UI/UXML/IconSheet.uxml";

    [MenuItem("Naija Streets/Build HUD Test Scenes")]
    public static void BuildAll()
    {
        var panelSettings = GetOrCreatePanelSettings();
        BuildHudTest(panelSettings);
        BuildIconSheet(panelSettings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[HUDSceneBuilder] Done.");
    }

    private static PanelSettings GetOrCreatePanelSettings()
    {
        var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (ps != null) return ps;
        ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.scaleMode = PanelScaleMode.ConstantPixelSize;
        ps.scale = 1f;
        ps.themeStyleSheet = null;
        AssetDatabase.CreateAsset(ps, PanelSettingsPath);
        Debug.Log("[HUDSceneBuilder] Created " + PanelSettingsPath);
        return ps;
    }

    private static void BuildHudTest(PanelSettings panelSettings)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "HUDTest";

        // Main camera: dark street backdrop, looking at the origin.
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.07f, 0.10f);
        cam.transform.position = new Vector3(0f, 6f, -8f);
        cam.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        camGo.AddComponent<AudioListener>();

        // Directional light so the greybox reads.
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Greybox street: asphalt plane + a few blocks + a capsule "player".
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground_Asphalt";
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        ground.GetComponent<Renderer>().sharedMaterial = GreyMat(new Color(0.16f, 0.16f, 0.18f));
        AddBlock("Block_A", new Vector3(-4f, 1f, 4f), new Vector3(3f, 2f, 3f), new Color(0.35f, 0.32f, 0.28f));
        AddBlock("Block_B", new Vector3(5f, 1.5f, 3f), new Vector3(3f, 3f, 3f), new Color(0.28f, 0.30f, 0.36f));
        AddBlock("Block_C", new Vector3(-5f, 0.75f, -4f), new Vector3(4f, 1.5f, 2f), new Color(0.30f, 0.34f, 0.30f));
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player_Marker";
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.GetComponent<Renderer>().sharedMaterial = GreyMat(new Color(0.18f, 0.62f, 0.39f));

        // HUD root.
        var hudGo = new GameObject("NLHud");
        var doc = hudGo.AddComponent<UIDocument>();
        doc.panelSettings = panelSettings;
        var uiRoot = hudGo.AddComponent<NLUIRoot>();
        var keys = hudGo.AddComponent<NLUIKeys>();
        // NLUIKeys needs the NLUIRoot for device tracking (private field).
        var keysSo = new SerializedObject(keys);
        keysSo.FindProperty("uiRoot").objectReferenceValue = uiRoot;
        keysSo.ApplyModifiedProperties();
        hudGo.AddComponent<NLStickControl>();
        hudGo.AddComponent<NLButtonControl>();

        // Radar camera (orthographic top-down) + NLRadar component.
        var radarCamGo = new GameObject("RadarCamera");
        var radarCam = radarCamGo.AddComponent<Camera>();
        radarCam.orthographic = true;
        radarCam.enabled = false; // NLRadar renders it manually at 10 Hz
        var radar = hudGo.AddComponent<NLRadar>();
        var radarSo = new SerializedObject(radar);
        radarSo.FindProperty("radarCamera").objectReferenceValue = radarCam;
        radarSo.ApplyModifiedProperties();

        var controller = hudGo.AddComponent<NLHudController>();
        var hudField = typeof(NLHudController).GetField("hudTemplate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var phoneField = typeof(NLHudController).GetField("phoneTemplate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        hudField.SetValue(controller, AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/UXML/NLHud.uxml"));
        phoneField.SetValue(controller, AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/UXML/NLPhone.uxml"));
        hudGo.AddComponent<NLUIScaler>();

        EditorSceneManager.SaveScene(scene, HudTestPath);
        Debug.Log("[HUDSceneBuilder] Saved " + HudTestPath);
    }

    private static void BuildIconSheet(PanelSettings panelSettings)
    {
        // Generated UXML: one tile per icon, name underneath.
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/UI/Icons" });
        var names = new List<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            names.Add(Path.GetFileNameWithoutExtension(path));
        }
        names.Sort();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">");
        sb.AppendLine("  <ui:VisualElement style=\"flex-direction: column; background-color: #0A0D14; padding: 16px;\">");
        sb.AppendLine("    <ui:Label text=\"Naija Streets icon sheet\" style=\"font-size: 24px; color: #F3F6F8; margin-bottom: 12px;\" />");
        sb.AppendLine("    <ui:VisualElement style=\"flex-direction: row; flex-wrap: wrap;\">");
        foreach (string name in names)
        {
            sb.AppendLine("      <ui:VisualElement style=\"width: 96px; height: 96px; flex-direction: column; align-items: center; justify-content: center; margin: 4px; background-color: rgba(255,255,255,0.05); border-radius: 12px;\">");
            sb.AppendLine($"        <ui:VisualElement style=\"width: 28px; height: 28px; background-image: url('project://database/Assets/UI/Icons/{name}.svg'); -unity-background-image-tint-color: #F3F6F8;\" />");
            sb.AppendLine($"        <ui:Label text=\"{name}\" style=\"font-size: 10px; color: #E1E8EE; margin-top: 6px;\" />");
            sb.AppendLine("      </ui:VisualElement>");
        }
        sb.AppendLine("    </ui:VisualElement>");
        sb.AppendLine("  </ui:VisualElement>");
        sb.AppendLine("</ui:UXML>");
        File.WriteAllText(IconSheetUxmlPath, sb.ToString());
        AssetDatabase.ImportAsset(IconSheetUxmlPath);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "IconSheet";
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
        var docGo = new GameObject("IconSheetUI");
        var doc = docGo.AddComponent<UIDocument>();
        doc.panelSettings = panelSettings;
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(IconSheetUxmlPath);
        doc.visualTreeAsset = uxml;
        EditorSceneManager.SaveScene(scene, IconSheetPath);
        Debug.Log($"[HUDSceneBuilder] Saved {IconSheetPath} ({names.Count} icons)");
    }

    private static Material GreyMat(Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        return mat;
    }

    private static void AddBlock(string name, Vector3 pos, Vector3 size, Color color)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.position = pos;
        block.transform.localScale = size;
        block.GetComponent<Renderer>().sharedMaterial = GreyMat(color);
    }
}
