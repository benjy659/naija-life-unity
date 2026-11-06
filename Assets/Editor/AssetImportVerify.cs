// ASSET IMPORT — VERIFICATION RUNNER (temporary editor tool, not part of the game).
// Builds a test scene in memory: ground, sun, camera + a sample of every imported
// set (landmark building, terminal, Makoko shack, Lagos kit, v2 player body, the
// wired Player prefab, danfo, okada, police pickup, BRT bus, Red Line train).
// Checks ALL 235 GLBs load as GameObjects, logs each sample's renderers / bounds /
// materials + shaders, then enters play mode and screenshots three angles.
// Entry: -executeMethod AssetImportVerify.Run (GUI mode under Xvfb; exits the Editor).
// Output: /var/cache/apt/archives/unity/asset-import/{results.txt, done.txt, shots/}.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AssetImportVerify
{
    private const string OutRoot = "/var/cache/apt/archives/unity/asset-import";
    private static string _outDir;
    private static double _watchdogStart;
    private const double TimeoutSeconds = 300;

    // Open the project (resolves packages, imports assets, compiles scripts),
    // then quit. Used for the first-open pass: -executeMethod AssetImportVerify.QuitAfterLoad
    // (batchmode is NOT usable on this VM — the Personal license lacks the
    // com.unity.editor.headless entitlement — so all Editor runs are GUI mode
    // under Xvfb and must exit via EditorApplication.Exit).
    public static void QuitAfterLoad()
    {
        Debug.Log("[AssetImport] first open complete (packages resolved, assets imported, scripts compiled) — quitting.");
        EditorApplication.Exit(0);
    }

    [MenuItem("Naija Life/Asset Import/Run Verify (screenshots)")]
    public static void Run()
    {
        _outDir = OutRoot;
        Directory.CreateDirectory(_outDir);
        Directory.CreateDirectory(Path.Combine(_outDir, "shots"));
        var log = new List<string> { $"ASSET IMPORT VERIFY {DateTime.UtcNow:o}" };

        // ---- 1. Every GLB must load as a GameObject. ----
        string[] glbs = Directory.GetFiles("Assets/Imported", "*.glb", SearchOption.AllDirectories);
        int ok = 0;
        var failed = new List<string>();
        foreach (string glb in glbs)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(glb);
            if (asset == null) failed.Add(glb);
            else ok++;
        }
        log.Add($"GLB load check: {ok}/{glbs.Length} load as GameObject");
        foreach (string f in failed) log.Add("  FAILED TO LOAD: " + f);
        Debug.Log($"[AssetImport] GLB load check: {ok}/{glbs.Length} OK");
        Resources.UnloadUnusedAssets();

        // ---- 2. Test scene. ----
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.intensity = 1.5f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.0f;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(8f, 1f, 8f);
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.color = new Color(0.16f, 0.16f, 0.18f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        var camGo = new GameObject("ShotCamera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 55f;
        SetGameViewSize(1280, 800);

        // ---- 3. Samples: one per set, spread over a 60x30 m yard. ----
        Place("Assets/Imported/Buildings/abuja/national_mosque.glb", new Vector3(-16f, 0f, -2f), 0f, log);
        Place("Assets/Imported/Buildings/terminal_1.glb", new Vector3(0f, 0f, -8f), 0f, log);
        Place("Assets/Imported/Buildings/makoko/stilt_shack_a.glb", new Vector3(12f, 0f, -6f), 0f, log);
        Place("Assets/Imported/Buildings/Lagos-kit.glb", new Vector3(20f, 0f, 4f), 0f, log);
        Place("Assets/Imported/Characters/Player/body_m.glb", new Vector3(0f, 0f, 5f), 180f, log);
        Place("Assets/Imported/Vehicles/Transit/danfo.glb", new Vector3(-7f, 0f, 7f), 0f, log);
        Place("Assets/Imported/Vehicles/okada.glb", new Vector3(-11f, 0f, 4f), 0f, log);
        Place("Assets/Imported/Vehicles/police_pickup.glb", new Vector3(-3f, 0f, 9f), 0f, log);
        Place("Assets/Imported/Vehicles/Transit/brt-12m.glb", new Vector3(-18f, 0f, 10f), 90f, log);
        Place("Assets/Imported/Vehicles/Transit/redline-dmu.glb", new Vector3(10f, 0f, 12f), 90f, log);

        // The wired Player prefab, next to the raw body_m for comparison.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        if (prefab == null)
        {
            log.Add("MISSING: Player prefab");
            Debug.LogError("[AssetImport] Player prefab not found");
        }
        else
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = "PlayerPrefabInstance";
            inst.transform.position = new Vector3(3f, 0f, 5f);
            inst.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            int childCount = inst.transform.childCount;
            string bodyM = inst.transform.Find("BodyM") != null ? "BodyM present" : "BodyM MISSING";
            log.Add($"Player prefab: instantiated, children={childCount}, {bodyM}");
            Debug.Log("[AssetImport] Player prefab instantiated: " + bodyM);
        }

        File.WriteAllLines(Path.Combine(_outDir, "results.txt"), log);

        // ---- 4. Screenshot harness (3 angles), then exit. ----
        var harnessGo = new GameObject("AssetImportVerifyHarness");
        var harness = harnessGo.AddComponent<AssetImportVerifyHarness>();
        harness.outDir = _outDir;
        harness.shotCam = cam;
        harness.camPositions = new[]
        {
            new Vector3(0f, 9f, -22f),   // wide: whole yard
            new Vector3(6f, 2.2f, -1f),  // close: character + danfo
            new Vector3(-22f, 7f, 10f),  // buildings: mosque + terminal
        };
        harness.camTargets = new[]
        {
            new Vector3(0f, 2f, 2f),
            new Vector3(1.5f, 1.0f, 5f),
            new Vector3(-8f, 3f, -4f),
        };

        _watchdogStart = EditorApplication.timeSinceStartup;
        EditorApplication.update += PollDone;
        EditorApplication.EnterPlaymode();
    }

    private static void Place(string path, Vector3 pos, float rotY, List<string> log)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            log.Add("MISSING: " + path);
            Debug.LogError("[AssetImport] MISSING model: " + path);
            return;
        }
        var inst = UnityEngine.Object.Instantiate(asset);
        inst.name = Path.GetFileNameWithoutExtension(path);
        inst.transform.position = pos;
        inst.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        var renderers = inst.GetComponentsInChildren<Renderer>(true);
        var mats = new HashSet<string>();
        foreach (Renderer r in renderers)
            foreach (Material m in r.sharedMaterials)
                if (m != null) mats.Add(m.name + " [" + (m.shader != null ? m.shader.name : "?") + "]");
        Bounds b = new Bounds(pos, Vector3.zero);
        bool first = true;
        foreach (Renderer r in renderers)
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        log.Add($"OK: {path} renderers={renderers.Length} boundsSize={b.size} center={b.center} materials={mats.Count}");
        foreach (string s in mats) log.Add("    mat: " + s);
        Debug.Log($"[AssetImport] placed {inst.name}: {renderers.Length} renderers, bounds {b.size}");
    }

    private static void PollDone()
    {
        string done = Path.Combine(_outDir, "done.txt");
        double elapsed = EditorApplication.timeSinceStartup - _watchdogStart;
        if (File.Exists(done))
        {
            Debug.Log("[AssetImport] Done marker found, exiting play mode.");
            EditorApplication.update -= PollDone;
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
            return;
        }
        if (elapsed > TimeoutSeconds)
        {
            Debug.LogError($"[AssetImport] TIMEOUT after {elapsed:0}s — no done marker. Quitting.");
            try { File.WriteAllText(Path.Combine(_outDir, "TIMEOUT.txt"), $"timeout after {elapsed:0}s"); } catch { }
            EditorApplication.update -= PollDone;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }

    // Same GameView sizing trick as M1VerifyRunner (reflection over internal API).
    private static void SetGameViewSize(int w, int h)
    {
        try
        {
            var sizes = GameViewSizes.instance;
            var size = new GameViewSize(GameViewSizeType.FixedResolution, w, h, "AssetImport");
            sizes.AddCustomSize(size);
            Type gvType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gvType == null) { Debug.LogWarning("[AssetImport] UnityEditor.GameView type not found"); return; }
            var gv = EditorWindow.GetWindow(gvType);
            gv.Show();
            int index = -1;
            try
            {
                var group = sizes.currentGroup;
                int builtin = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                var customs = (IList)group.GetType().GetMethod("GetCustomSizes").Invoke(group, null);
                index = builtin + customs.Count - 1;
            }
            catch (Exception e) { Debug.LogWarning("[AssetImport] size index lookup failed: " + e.Message); }
            if (index >= 0)
            {
                var prop = gvType.GetProperty("currentSizeIndex",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanWrite) prop.SetValue(gv, index);
                else
                {
                    var field = gvType.GetField("m_currentSizeIndex",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null) field.SetValue(gv, index);
                }
                gv.Repaint();
            }
            Debug.Log($"[AssetImport] GameView size set to {w}x{h}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AssetImport] SetGameViewSize failed (viewport may be wrong size): " + e);
        }
    }
}
