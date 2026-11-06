// ASSET IMPORT — VERIFICATION RUNNER (temporary editor tool, not part of the game).
// Builds a test scene in memory: ground, sun, camera + a sample of every imported
// set (landmark building, terminal, Makoko shack, Lagos kit, v2 player body, the
// wired Player prefab, danfo, okada, police pickup, BRT bus, Red Line train).
// Checks ALL 235 GLBs load as GameObjects, logs each sample's renderers / bounds /
// materials + shaders, then screenshots three angles IN EDIT MODE (no play mode —
// see section 4 note) and exits.
// Entry: -executeMethod AssetImportVerify.Run (GUI mode under Xvfb; exits the Editor).
// Output: /var/cache/apt/archives/unity/asset-import/{results.txt, done.txt, shots/}.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AssetImportVerify
{
    private const string OutRoot = "/var/cache/apt/archives/unity/asset-import";
    private static string _outDir;

    // Open the project (resolves packages, imports assets, compiles scripts),
    // then quit. Used for the first-open pass: -executeMethod AssetImportVerify.QuitAfterLoad
    // GUI mode under Xvfb (the earlier batchmode attempts died on license
    // validation while the VM-refresh machine-id issue was unresolved; the
    // license is re-activated now, but GUI mode is the proven path on this VM).
    // Every GUI Editor launch as root stops at a "running Unity as root"
    // dialog — dismiss it by clicking "Continue anyway" (button at ~236,98 on :99).
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

        // No ground plane: the throwaway plane's URP/Lit material renders
        // MAGENTA in the edit-mode capture (shader not compiled for the
        // RenderTexture path) — a test-scene artifact, not an imported asset.
        // The skybox gradient is the background instead (as in the mosque shot).

        var camGo = new GameObject("ShotCamera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 55f;
        // NOTE: no GameView sizing — GameViewSizes/GameViewSize are internal in
        // Unity 6000.3 (the M1VerifyRunner's reflection approach does not compile
        // here). Screenshots are taken by rendering the camera to a 1280x800
        // RenderTexture (see AssetImportVerifyHarness), independent of window size.

        // ---- 3. Samples: one per set, spread over a 60x30 m yard. ----
        // The National Mosque is 121 m wide / 64 m tall — it DWARFS the yard
        // and swallows any camera placed near it (shots 01/02 of the first
        // verify run were taken from inside it). Park it far away so the
        // yard cameras frame the terminal/shack/kit/character/vehicles, and
        // give it its own distant shot instead.
        Place("Assets/Imported/Buildings/abuja/national_mosque.glb", new Vector3(-140f, 0f, -40f), 0f, log);
        Place("Assets/Imported/Buildings/terminal_1.glb", new Vector3(0f, 0f, -8f), 0f, log);
        Place("Assets/Imported/Buildings/makoko/stilt_shack_a.glb", new Vector3(12f, 0f, -6f), 0f, log);
        Place("Assets/Imported/Buildings/Lagos-kit.glb", new Vector3(20f, 0f, 4f), 0f, log);
        Place("Assets/Imported/Characters/Player/body_m.glb", new Vector3(28f, 0f, 2f), 180f, log);
        Place("Assets/Imported/Vehicles/Transit/danfo.glb", new Vector3(25f, 0f, 8f), 0f, log);
        Place("Assets/Imported/Vehicles/okada.glb", new Vector3(33f, 0f, 6f), 0f, log);
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
            inst.transform.position = new Vector3(31f, 0f, 2f);
            inst.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            int childCount = inst.transform.childCount;
            string bodyM = inst.transform.Find("BodyM") != null ? "BodyM present" : "BodyM MISSING";
            log.Add($"Player prefab: instantiated, children={childCount}, {bodyM}");
            Debug.Log("[AssetImport] Player prefab instantiated: " + bodyM);
        }

        File.WriteAllLines(Path.Combine(_outDir, "results.txt"), log);

        // ---- 4. Screenshots (3 angles), then exit. ----
        // NOTE: done WITHOUT entering play mode. The first verify run used
        // a play-mode harness; its EnterPlaymode() hit the Input System
        // "Enable & Restart / Don't Enable" modal (the project hasn't set
        // Active Input Handling) and the second run's Editor shut down
        // during play-mode entry under VM saturation (load ~45, three
        // Editors) with no screenshots. Camera.Render() works in edit mode
        // and renders the same URP scene — no dialog, no domain reload.
        var positions = new[]
        {
            // Yard cameras must stay OUTSIDE terminal_1's footprint (70.75 m
            // wide, x -44..+27, z -26..+10): the first re-run's cameras were
            // inside the terminal and shot mostly interior white + glass.
            new Vector3(30f, 14f, -35f),   // wide: whole yard + terminal facade
            new Vector3(29f, 3f, -6f),     // close: character cluster (east clear area, outside terminal + train)
            new Vector3(-95f, 30f, -75f),  // landmark: National Mosque from a distance
        };
        var targets = new[]
        {
            new Vector3(-5f, 3f, 5f),
            new Vector3(29f, 1f, 3f),
            new Vector3(-140f, 25f, -45f),
        };
        for (int i = 0; i < positions.Length; i++)
        {
            cam.transform.position = positions[i];
            cam.transform.LookAt(targets[i]);
            string path = Path.Combine(_outDir, "shots", $"0{i + 1}-asset-import.png");
            CaptureToPng(cam, 1280, 800, path);
            Debug.Log("[AssetImport] screenshot: " + path);
        }
        File.WriteAllText(Path.Combine(_outDir, "done.txt"), DateTime.UtcNow.ToString("o"));
        Debug.Log("[AssetImport] verify done.");
        EditorApplication.Exit(0);
    }

    private static void CaptureToPng(Camera cam, int w, int h, string path)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = prevTarget;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
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

}
