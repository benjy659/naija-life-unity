// M1 VERIFICATION RUNNER — temporary editor-only script, NOT part of the game.
// Entry points (via -executeMethod):
//   RunDesktop : play Greybox_M1 at 1280x800, full harness
//   RunPhone   : play Greybox_M1 at 390x844, short harness
//   BuildWebGL : build Greybox_M1 for WebGL (scene NOT in Build Settings;
//                passed explicitly via BuildPlayerOptions.scenes)
// Deletes itself after the run (the verifier removes this file).
// NOTE: adds a "PlayerCamera (M1 verify)" GameObject to the OPEN scene in
// memory (the committed scene never got the M1.2 wiring step). Play runs do
// NOT save the scene; BuildWebGL DOES save it (local only, uncommitted) so
// the build contains the camera rig, and the verifier reverts it after.

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NaijaLife.Player;
using NaijaLife.Verification;

public static class M1VerifyRunner
{
    private const string ScenePath = "Assets/Scenes/Greybox_M1.unity";
    private const string OutRoot = "/var/cache/apt/archives/unity/m1-verify";
    private static string _outDir;
    private static double _watchdogStart;
    private static double _timeoutSeconds = 420;

    // ---------------- entry points ----------------

    public static void RunDesktop()
    {
        _timeoutSeconds = 420;
        RunPlay(1280, 800, false);
    }

    public static void RunPhone()
    {
        _timeoutSeconds = 240;
        RunPlay(390, 844, true);
    }

    public static void BuildWebGL()
    {
        string outDir = Path.Combine(OutRoot, "webgl-build");
        string logPath = Path.Combine(OutRoot, "webgl-build.log");
        try
        {
            Directory.CreateDirectory(OutRoot);
            using (var log = new StreamWriter(logPath, false))
            {
                void L(string s) { Debug.Log("[M1Build] " + s); log.WriteLine(s); log.Flush(); }

                L("BuildWebGL started " + DateTime.UtcNow.ToString("o"));
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EnsureCameraRigInScene(L);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                L("Scene saved WITH the PlayerCamera rig (local only, uncommitted).");

                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                var origCompression = PlayerSettings.WebGL.compressionFormat;
                L($"Original WebGL compressionFormat: {origCompression}");
                // GitHub Pages does not serve .gz/.br with Content-Encoding, so a
                // compressed build would fail to load there. Ship uncompressed.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                L("Set WebGL compressionFormat=Disabled for GitHub Pages compatibility.");

                if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outDir,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                };
                L("Building to " + outDir);
                var report = BuildPipeline.BuildPlayer(opts);
                L($"Build result: {report.summary.result} in {report.summary.totalTime.TotalSeconds:0.0}s, " +
                  $"errors={report.summary.totalErrors} warnings={report.summary.totalWarnings}");
                foreach (var step in report.steps)
                    foreach (var msg in step.messages)
                        if (msg.type == LogType.Error || msg.type == LogType.Warning)
                            L($"  [{msg.type}] {msg.content}");

                PlayerSettings.WebGL.compressionFormat = origCompression;
                L("Restored compressionFormat to " + origCompression);

                // Sizes.
                long total = 0;
                foreach (var f in Directory.GetFiles(outDir, "*", SearchOption.AllDirectories))
                {
                    long len = new FileInfo(f).Length;
                    total += len;
                    L($"  {f.Substring(outDir.Length + 1)} {len / 1024.0:0.0} KB");
                }
                L($"TOTAL BUILD SIZE: {total / 1024.0 / 1024.0:0.00} MB");
                L(report.summary.result == BuildResult.Succeeded
                    ? "BUILD SUCCEEDED" : "BUILD FAILED");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[M1Build] EXCEPTION: " + e);
            File.WriteAllText(logPath, "EXCEPTION: " + e);
        }
        finally
        {
            EditorApplication.Exit(0);
        }
    }

    // ---------------- play-mode driver ----------------

    private static void RunPlay(int w, int h, bool phone)
    {
        _outDir = Path.Combine(OutRoot, phone ? "phone" : "desktop");
        Directory.CreateDirectory(_outDir);
        Directory.CreateDirectory(Path.Combine(_outDir, "shots"));
        Debug.Log($"[M1Verify] RunPlay {w}x{h} phone={phone} out={_outDir}");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureCameraRigInScene(s => Debug.Log("[M1Verify] " + s));
        SetGameViewSize(w, h);

        var harnessGo = new GameObject("M1VerifyHarness");
        var harness = harnessGo.AddComponent<M1VerifyHarness>();
        harness.outDir = _outDir;
        harness.phoneRun = phone;

        _watchdogStart = EditorApplication.timeSinceStartup;
        EditorApplication.update += PollDone;
        EditorApplication.EnterPlaymode();
    }

    private static void PollDone()
    {
        string done = Path.Combine(_outDir, "done.txt");
        double elapsed = EditorApplication.timeSinceStartup - _watchdogStart;
        if (File.Exists(done))
        {
            Debug.Log("[M1Verify] Done marker found, exiting play mode.");
            EditorApplication.update -= PollDone;
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
            return;
        }
        if (elapsed > _timeoutSeconds)
        {
            Debug.LogError($"[M1Verify] TIMEOUT after {elapsed:0}s — no done marker. Quitting.");
            try { File.WriteAllText(Path.Combine(_outDir, "TIMEOUT.txt"), $"timeout after {elapsed:0}s"); } catch { }
            EditorApplication.update -= PollDone;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }

    // The committed scene never got the M1.2 wiring step (no PlayerCamera
    // GameObject). Add it in memory per docs/specs/camera-m1.2.md wiring.
    private static void EnsureCameraRigInScene(Action<string> log)
    {
        var existing = UnityEngine.Object.FindFirstObjectByType<PlayerCamera>();
        if (existing != null) { log("PlayerCamera already present in scene."); return; }
        var go = new GameObject("PlayerCamera (M1 verify)");
        go.AddComponent<PlayerCamera>();
        log("Added 'PlayerCamera (M1 verify)' GameObject (PlayerCamera component, auto-finds Player tag).");
    }

    // ---------------- Game view sizing ----------------

    private static void SetGameViewSize(int w, int h)
    {
        try
        {
            // GameViewSizes / GameViewSize / GameViewSizeType are INTERNAL in
            // Unity 6 — everything here goes through reflection (the direct
            // calls do not compile). AddCustomSize lives on the GROUP, not on
            // GameViewSizes (the old sizes.AddCustomSize call NRE'd — the
            // method does not exist there). The count method is
            // GetCustomCount; there is no GetCustomSizes. API pattern proven
            // by the HUD verify runner (hud-phone-ui).
            Type sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            Type sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
            Type sizeTypeEnum = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
            if (sizesType == null || sizeType == null || sizeTypeEnum == null)
            {
                Debug.LogWarning("[M1Verify] GameViewSizes types not found (viewport may be wrong size)");
                return;
            }
            // GameViewSizes inherits `instance` from
            // ScriptableSingleton<GameViewSizes>, so FlattenHierarchy is
            // required or GetProperty returns null.
            object sizes = sizesType.GetProperty("instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)?.GetValue(null);
            if (sizes == null) { Debug.LogWarning("[M1Verify] GameViewSizes.instance is null"); return; }
            object group = sizesType.GetProperty("currentGroup",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(sizes);
            if (group == null)
            {
                var getGroup = sizesType.GetMethod("GetGroup",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var groupType = sizesType.GetProperty("currentGroupType",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(sizes);
                if (getGroup != null && groupType != null)
                    group = getGroup.Invoke(sizes, new[] { groupType });
            }
            if (group == null) { Debug.LogWarning("[M1Verify] GameView size group is null; viewport may be wrong size"); return; }
            object fixedRes = Enum.Parse(sizeTypeEnum, "FixedResolution");
            object size = Activator.CreateInstance(sizeType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                new object[] { fixedRes, w, h, "M1Verify" }, null);
            var addCustom = group.GetType().GetMethod("AddCustomSize",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (addCustom == null) { Debug.LogWarning("[M1Verify] AddCustomSize not found on group"); return; }
            addCustom.Invoke(group, new[] { size });
            Type gvType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gvType == null) { Debug.LogWarning("[M1Verify] UnityEditor.GameView type not found"); return; }
            var gv = EditorWindow.GetWindow(gvType);
            gv.Show();
            int index = -1;
            try
            {
                var builtinMethod = group.GetType().GetMethod("GetBuiltinCount",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var customMethod = group.GetType().GetMethod("GetCustomCount",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (builtinMethod == null || customMethod == null) { Debug.LogWarning("[M1Verify] GameView size count methods not found"); return; }
                int builtin = (int)builtinMethod.Invoke(group, null);
                int customs = (int)customMethod.Invoke(group, null);
                index = builtin + customs - 1;
            }
            catch (Exception e) { Debug.LogWarning("[M1Verify] size index lookup failed: " + e.Message); }
            if (index >= 0)
            {
                var prop = gvType.GetProperty("currentSizeIndex",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanWrite) prop.SetValue(gv, index);
                else
                {
                    var field = gvType.GetField("m_currentSizeIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null) field.SetValue(gv, index);
                }
                gv.Repaint();
                Debug.Log($"[M1Verify] GameView size index set to {index} ({w}x{h})");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[M1Verify] SetGameViewSize failed (viewport may be wrong size): " + e);
        }
    }
}
