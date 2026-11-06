// HUD VERIFICATION RUNNER — temporary editor-only script, NOT part of the game.
// Entry points (via -executeMethod), one per viewport:
//   RunPhonePortrait  : HUDTest at 390x844
//   RunPhoneLandscape : HUDTest at 844x390
//   RunDesktop1280    : HUDTest at 1280x800
//   RunDesktop1920    : HUDTest at 1920x1080
//   RunIconSheet      : IconSheet at 1280x800 (single screenshot)
//   BuildFontsAndScenes : HUDFontBuilder + HUDSceneBuilder (no play mode)
// Mirrors M1VerifyRunner's GameView-size + done-marker pattern.

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NaijaLife.Verification;

public static class HUDVerifyRunner
{
    private const string ScenePath = "Assets/Scenes/HUDTest.unity";
    private const string IconSheetPath = "Assets/Scenes/IconSheet.unity";
    private const string OutRoot = "/var/cache/apt/archives/unity/hud-verify";
    private static string _outDir;
    private static double _watchdogStart;
    private static double _timeoutSeconds = 240;

    public static void BuildFontsAndScenes()
    {
        try
        {
            // Fonts are the Inter TTFs referenced directly from NLTokens.uss
            // (-unity-font). No SDF build step: TMP_FontAsset is the wrong type
            // for -unity-font-definition (it needs a TextCore FontAsset), and
            // TMP_FontAsset.CreateFontAsset NREs in this project (no TMP
            // Settings asset). Direct TTF is the standard UI Toolkit path.
            HUDSceneBuilder.BuildAll();
            Debug.Log("[HUDVerify] Scenes built (fonts are direct TTF).");
        }
        catch (Exception e)
        {
            Debug.LogError("[HUDVerify] BuildFontsAndScenes EXCEPTION: " + e);
        }
        finally
        {
            EditorApplication.Exit(0);
        }
    }

    public static void RunPhonePortrait() { RunPlay(390, 844, "phone-portrait"); }
    public static void RunPhoneLandscape() { RunPlay(844, 390, "phone-landscape"); }
    public static void RunDesktop1280() { RunPlay(1280, 800, "desktop-1280"); }
    public static void RunDesktop1920() { RunPlay(1920, 1080, "desktop-1920"); }

    public static void RunIconSheet()
    {
        _outDir = Path.Combine(OutRoot, "icon-sheet");
        Directory.CreateDirectory(_outDir);
        Directory.CreateDirectory(Path.Combine(_outDir, "shots"));
        EditorSceneManager.OpenScene(IconSheetPath, OpenSceneMode.Single);
        SetGameViewSize(1280, 800);
        var harnessGo = new GameObject("HUDVerifyHarness");
        var harness = harnessGo.AddComponent<HUDVerifyHarness>();
        harness.outDir = _outDir;
        harness.iconSheetRun = true;
        _watchdogStart = EditorApplication.timeSinceStartup;
        EditorApplication.update += PollDone;
        EditorApplication.EnterPlaymode();
    }

    private static void RunPlay(int w, int h, string label)
    {
        _outDir = Path.Combine(OutRoot, label);
        Directory.CreateDirectory(_outDir);
        Directory.CreateDirectory(Path.Combine(_outDir, "shots"));
        Debug.Log($"[HUDVerify] RunPlay {w}x{h} out={_outDir}");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SetGameViewSize(w, h);

        var harnessGo = new GameObject("HUDVerifyHarness");
        var harness = harnessGo.AddComponent<HUDVerifyHarness>();
        harness.outDir = _outDir;

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
            Debug.Log("[HUDVerify] Done marker found, exiting play mode.");
            EditorApplication.update -= PollDone;
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
            return;
        }
        if (elapsed > _timeoutSeconds)
        {
            Debug.LogError($"[HUDVerify] TIMEOUT after {elapsed:0}s — no done marker. Quitting.");
            try { File.WriteAllText(Path.Combine(_outDir, "TIMEOUT.txt"), $"timeout after {elapsed:0}s"); } catch { }
            EditorApplication.update -= PollDone;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
    }

    private static void SetGameViewSize(int w, int h)
    {
        try
        {
            // GameViewSizes / GameViewSize / GameViewSizeType are INTERNAL in
            // Unity 6 — everything here goes through reflection (the direct
            // calls do not compile). Mirrors the M1 runner's pattern, but
            // fully reflected: the M1 runner's direct calls fail the same way.
            Type sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            Type sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
            Type sizeTypeEnum = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
            if (sizesType == null || sizeType == null || sizeTypeEnum == null)
            {
                Debug.LogWarning("[HUDVerify] GameViewSizes types not found (viewport may be wrong size)");
                return;
            }
            // GameViewSizes inherits `instance` from
            // ScriptableSingleton<GameViewSizes>, so FlattenHierarchy is
            // required or GetProperty returns null (the "instance is null"
            // failure from the first phone-portrait run).
            object sizes = sizesType.GetProperty("instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)?.GetValue(null);
            if (sizes == null) { Debug.LogWarning("[HUDVerify] GameViewSizes.instance is null"); return; }
            object fixedRes = Enum.Parse(sizeTypeEnum, "FixedResolution");
            object size = Activator.CreateInstance(sizeType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                new object[] { fixedRes, w, h, "HUDVerify" }, null);
            sizesType.GetMethod("AddCustomSize",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(sizes, new[] { size });
            Type gvType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gvType == null) { Debug.LogWarning("[HUDVerify] UnityEditor.GameView type not found"); return; }
            var gv = EditorWindow.GetWindow(gvType);
            gv.Show();
            int index = -1;
            try
            {
                object group = sizesType.GetProperty("currentGroup",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(sizes);
                if (group == null)
                {
                    // currentGroup can be null before the GameView initializes
                    // its group; ask for the selected build target's group.
                    var getGroup = sizesType.GetMethod("GetGroup",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (getGroup != null)
                        group = getGroup.Invoke(sizes, new object[] { EditorUserBuildSettings.selectedBuildTargetGroup });
                }
                if (group == null) { Debug.LogWarning("[HUDVerify] GameView size group is null; viewport may be wrong size"); return; }
                var builtinMethod = group.GetType().GetMethod("GetBuiltinCount");
                var customMethod = group.GetType().GetMethod("GetCustomSizes");
                if (builtinMethod == null || customMethod == null) { Debug.LogWarning("[HUDVerify] GameView size group methods not found"); return; }
                int builtin = (int)builtinMethod.Invoke(group, null);
                var customs = (IList)customMethod.Invoke(group, null);
                index = builtin + customs.Count - 1;
            }
            catch (Exception e) { Debug.LogWarning("[HUDVerify] size index lookup failed: " + e.Message); }
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
                Debug.Log($"[HUDVerify] GameView size index set to {index} ({w}x{h})");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[HUDVerify] SetGameViewSize failed (viewport may be wrong size): " + e);
        }
    }
}
