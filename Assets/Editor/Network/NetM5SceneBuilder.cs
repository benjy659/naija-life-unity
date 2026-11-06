// M5.1d scene builder (Editor only). Builds the two-client smoke-test scene so
// Ted's team does not wire it by hand. Spec: docs/specs/netcode-m5.md §7.1.
//
// Menu: Naija Streets > Network > Build Net_M5 scene
// Batch: Unity -batchmode -projectPath . -executeMethod NetM5SceneBuilder.Build -quit
//        Unity -batchmode -projectPath . -executeMethod NetM5SceneBuilder.BuildWebGL -quit
//
// What it does (safe to run again; it rebuilds the same files):
//   1. Copies Assets/Scenes/Greybox_M1.unity to Assets/Scenes/Net_M5.unity.
//   2. Makes Assets/Prefabs/Network/RemotePlayer.prefab: an orange capsule
//      (no collider) with a TextMeshPro name tag, rich text off.
//   3. In Net_M5: adds "Net" (NetBootstrap, district oshodi, RemotePlayerSpawner),
//      and puts NetPlayerBridge on the Player. With NL_FUSION it also adds
//      FusionSessionHost (Ted assigns the NetAvatar prefab ref after M5.1a).
// It never touches Greybox_M1 itself and never writes keys: the Supabase anon
// key and Photon App ID are pasted locally by Ted's team.
// UNVERIFIED (2026-11-06): written without the Editor; not compiled yet.

using System.IO;
using NaijaLife.NetworkGlue;
using NaijaLife.Player;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NetM5SceneBuilder
{
    const string SourceScene = "Assets/Scenes/Greybox_M1.unity";
    const string ScenePath = "Assets/Scenes/Net_M5.unity";
    const string PrefabDir = "Assets/Prefabs/Network";
    const string PrefabPath = PrefabDir + "/RemotePlayer.prefab";
    const string MaterialPath = PrefabDir + "/RemotePlayer.mat";

    [MenuItem("Naija Streets/Network/Build Net_M5 scene")]
    public static void Build()
    {
        if (!File.Exists(SourceScene)) { Debug.LogError("[M5.1d] missing " + SourceScene); return; }
        GameObject remote = BuildRemotePrefab();

        AssetDatabase.CopyAsset(SourceScene, ScenePath); // overwrites an older copy
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null) { Debug.LogError("[M5.1d] no PlayerController in " + SourceScene); return; }
        if (!player.TryGetComponent(out NetPlayerBridge _)) player.gameObject.AddComponent<NetPlayerBridge>();

        GameObject net = GameObject.Find("Net") ?? new GameObject("Net");
        if (!net.TryGetComponent(out NetBootstrap boot)) boot = net.AddComponent<NetBootstrap>();
        var so = new SerializedObject(boot);
        so.FindProperty("city").stringValue = "lagos";
        so.FindProperty("district").stringValue = "oshodi";
        so.ApplyModifiedPropertiesWithoutUndo();

#if NL_FUSION
        if (!net.TryGetComponent(out NaijaLife.Network.Fusion.FusionSessionHost _))
            net.AddComponent<NaijaLife.Network.Fusion.FusionSessionHost>();
        Debug.Log("[M5.1d] FusionSessionHost added: assign AvatarPrefab (NetAvatar prefab) in the Inspector.");
#endif

        if (!net.TryGetComponent(out RemotePlayerSpawner spawner)) spawner = net.AddComponent<RemotePlayerSpawner>();
        var sp = new SerializedObject(spawner);
        sp.FindProperty("remotePrefab").objectReferenceValue = remote;
        sp.FindProperty("localPlayer").objectReferenceValue = player.transform;
        sp.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[M5.1d] built " + ScenePath + ". Paste the Supabase anon key on Net > NetBootstrap locally (do not commit it).");
    }

    /// <summary>Builds Net_M5 for WebGL into Builds/WebGL-Net_M5 (scene passed explicitly).</summary>
    public static void BuildWebGL()
    {
        Build();
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            target = BuildTarget.WebGL,
            locationPathName = "Builds/WebGL-Net_M5",
            options = BuildOptions.Development, // dev build shows the status badge and event logs
        };
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("[M5.1d] WebGL build: " + report.summary.result + ", " + report.summary.totalSize / (1024 * 1024) + " MB");
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    static GameObject BuildRemotePrefab()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "Network");
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(lit) { color = new Color(0.95f, 0.55f, 0.15f) }; // orange: easy to tell from the local player
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        var root = new GameObject("RemotePlayer");
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>()); // remote players never block the local one in M5.1
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f); // root at the feet, like the pose
        body.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var tagGo = new GameObject("NameTag");
        tagGo.transform.SetParent(root.transform, false);
        tagGo.transform.localPosition = new Vector3(0f, 2.25f, 0f);
        var tag = tagGo.AddComponent<TextMeshPro>();
        tag.richText = false;
        tag.text = "Player";
        tag.fontSize = 3f;
        tag.alignment = TextAlignmentOptions.Center;
        tag.rectTransform.sizeDelta = new Vector2(4f, 0.8f);
        tagGo.AddComponent<FaceCamera>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }
}
