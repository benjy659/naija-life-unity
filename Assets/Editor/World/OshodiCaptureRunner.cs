// M2.1 / M2.2 QA capture runner (docs/specs/oshodi-greybox.md section 11).
// Takes the screenshots Ted's team would otherwise take by hand, from the
// viewpoint markers in the build list, and writes a short report next to them.
//
//   qa/M2.1/V1..V5-390x844.png and -1280x800.png   (Medium tier, 13:00 WAT)
//   qa/M2.2/<Low|Medium|High>/V1-0700.png .. V1-2100.png (1280x800)
//   qa/M2.2/<tier>/V6-2100-390x844.png               (night, phone size)
//   qa/M2.1/capture-report.md, qa/M2.2/capture-report.md
//
// Batch mode (needs a GPU: do NOT pass -nographics):
//   Unity -batchmode -projectPath . -executeMethod NaijaLife.World.Editor.OshodiCaptureRunner.RunAllBatch -logFile -
//     builds both scenes, sets up Bootstrap, then captures
//   ... -executeMethod NaijaLife.World.Editor.OshodiCaptureRunner.CaptureBatch   (capture only)
// Menu: Naija Streets > Capture Oshodi QA shots
//
// The scene is opened, lit and shot in memory and never saved. The "objects in
// view" numbers are an estimate from renderer bounds (renderers in the camera
// frustum, and unique mesh + material pairs, which is roughly the draw calls
// after batching). The real draw call number still comes from the Game view
// Stats panel or the Frame Debugger at each viewpoint. These are camera-only
// Editor frames: a fast first pass, not a replacement for the WebGL walk shots.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NaijaLife.World.Editor
{
    public static class OshodiCaptureRunner
    {
        static readonly (int w, int h)[] Sizes = { (390, 844), (1280, 800) };
        static readonly float[] QaHours = { 7f, 13f, 17.5f, 21f };
        const float EyeHeight = 1.6f;
        const float PitchDown = 3f;

        [MenuItem("Naija Streets/Capture Oshodi QA shots")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.DisplayDialog("Oshodi QA shots", Capture(), "OK");
        }

        public static void RunAllBatch()
        {
            Batch(() =>
            {
                Debug.Log("[Oshodi] " + OshodiGreyboxBuilder.BuildAll());
                Debug.Log("[Oshodi] " + OshodiBootstrapSetup.Setup());
                return Capture();
            });
        }

        public static void CaptureBatch() => Batch(Capture);

        static void Batch(Func<string> run)
        {
            try
            {
                Debug.Log("[Oshodi] " + run());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[Oshodi] capture failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static string Capture()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("No graphics device. Run Unity without -nographics so the camera can render.");

            var data = GreyboxBuildFile.Load();
            if (!File.Exists(OshodiGreyboxBuilder.CoreScenePath)) OshodiGreyboxBuilder.BuildAll();
            var scene = EditorSceneManager.OpenScene(OshodiGreyboxBuilder.CoreScenePath, OpenSceneMode.Single);

            // Hide the playtest player and camera so they are not in the shots; keep the sun.
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == OshodiGreyboxBuilder.PlaytestRoot)
                    foreach (Transform child in root.transform)
                        if (child.GetComponent<Light>() == null) child.gameObject.SetActive(false);

            var tod = UnityEngine.Object.FindFirstObjectByType<TimeOfDay>();
            if (tod == null) throw new InvalidOperationException("No TimeOfDay in the core scene. Rebuild with Naija Streets > Build Oshodi greybox.");
            var pool = UnityEngine.Object.FindFirstObjectByType<LampLightPool>();
            var nightOnly = UnityEngine.Object.FindObjectsByType<NightOnly>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var views = data.markers.Where(m => m.scene == "core" && m.group == "Markers/Viewpoints").OrderBy(m => m.id).ToList();
            if (views.Count == 0) throw new InvalidOperationException("No viewpoint markers in the build list.");

            var camGo = new GameObject("QA Capture Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 600f;
            cam.fieldOfView = 60f;
            cam.enabled = false;   // rendered by hand only

            string qa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "qa"));
            int startLevel = QualitySettings.GetQualityLevel();
            var r1 = new StringBuilder();
            var r2 = new StringBuilder();
            int shots = 0;

            try
            {
                // ---- M2.1: every viewpoint except the night one, Medium tier, 13:00
                string dir1 = Path.Combine(qa, "M2.1");
                Directory.CreateDirectory(dir1);
                Header(r1, "M2.1 viewpoint captures", "Medium tier, 13:00 WAT. Check each shot against spec section 1 and the viewpoint notes in docs/specs/oshodi-greybox-layout.json.");
                SetTier(1);
                foreach (var v in views.Where(v => v.id != "V6"))
                    foreach (var size in Sizes)
                    {
                        int w = size.w, h = size.h;
                        string file = $"{v.id}-{w}x{h}.png";
                        var est = Shoot(cam, v, 13f, w, h, tod, pool, nightOnly, Path.Combine(dir1, file));
                        r1.AppendLine($"| {file} | {v.id} | ({v.x:F1}, {v.y:F2}, {v.z:F1}) facing {v.ry:F0} | {est.renderers} | {est.pairs} |");
                        shots++;
                    }

                // ---- M2.2: V1 at each QA hour on each tier, plus V6 at night at phone size
                string dir2 = Path.Combine(qa, "M2.2");
                Directory.CreateDirectory(dir2);
                Header(r2, "M2.2 tier and time-of-day captures", "V1 at 07:00, 13:00, 17:30 and 21:00 on each tier (1280x800), and V6 (21:00) at 390x844. Night must stay readable at phone size.");
                var v1 = views.First(v => v.id == "V1");
                var v6 = views.FirstOrDefault(v => v.id == "V6") ?? v1;
                string[] names = QualitySettings.names;
                for (int tier = 0; tier < names.Length; tier++)
                {
                    SetTier(tier);
                    string tdir = Path.Combine(dir2, names[tier]);
                    Directory.CreateDirectory(tdir);
                    foreach (var hour in QaHours)
                    {
                        string file = $"{names[tier]}/V1-{HourTag(hour)}.png";
                        var est = Shoot(cam, v1, hour, 1280, 800, tod, pool, nightOnly, Path.Combine(dir2, file));
                        r2.AppendLine($"| {file} | V1 | {HourTag(hour)} | {est.renderers} | {est.pairs} |");
                        shots++;
                    }
                    string night = $"{names[tier]}/V6-2100-390x844.png";
                    var e6 = Shoot(cam, v6, 21f, 390, 844, tod, pool, nightOnly, Path.Combine(dir2, night));
                    r2.AppendLine($"| {night} | V6 | 2100 | {e6.renderers} | {e6.pairs} |");
                    shots++;
                }
            }
            finally
            {
                SetTier(startLevel);
                UnityEngine.Object.DestroyImmediate(camGo);
            }

            File.WriteAllText(Path.Combine(qa, "M2.1", "capture-report.md"), r1.ToString());
            File.WriteAllText(Path.Combine(qa, "M2.2", "capture-report.md"), r2.ToString());
            // The scene was changed in memory only (lights, pool, hidden playtest objects): reopen it clean.
            EditorSceneManager.OpenScene(OshodiGreyboxBuilder.CoreScenePath, OpenSceneMode.Single);
            return $"{shots} shots written under {qa}/M2.1 and {qa}/M2.2 with capture-report.md in each.";
        }

        static void Header(StringBuilder sb, string title, string what)
        {
            sb.AppendLine("# " + title);
            sb.AppendLine();
            sb.AppendLine($"Made by OshodiCaptureRunner on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC, Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceName}.");
            sb.AppendLine(what);
            sb.AppendLine("These are camera-only Editor frames (spec section 11, item 2 label). They are a fast first pass; the WebGL shots with the player walked to each spot are still needed.");
            sb.AppendLine("\"Renderers\" and \"mesh+material pairs\" are estimates from bounds; take the real draw call count from Stats or the Frame Debugger.");
            sb.AppendLine();
            sb.AppendLine(title.StartsWith("M2.1")
                ? "| File | View | Where | Renderers in view | Mesh+material pairs |\n|---|---|---|---|---|"
                : "| File | View | WAT | Renderers in view | Mesh+material pairs |\n|---|---|---|---|---|");
        }

        static string HourTag(float h) => $"{(int)h:00}{(int)Mathf.Round((h - (int)h) * 60f):00}";

        static void SetTier(int level) => QualitySettings.SetQualityLevel(level, true);

        static (int renderers, int pairs) Shoot(Camera cam, GreyboxMarker v, float hour, int w, int h,
            TimeOfDay tod, LampLightPool pool, NightOnly[] nightOnly, string path)
        {
            cam.transform.SetPositionAndRotation(new Vector3(v.x, v.y + EyeHeight, v.z), Quaternion.Euler(PitchDown, v.ry, 0f));
            cam.aspect = (float)w / h;

            tod.overrideHour = hour;
            tod.ApplyNow();
            bool night = tod.Night01 > 0.5f;
            if (pool != null)
            {
                pool.Rescan();   // picks up the pool size for the current tier
                pool.RefreshNow(night, cam.transform.position);
            }
            foreach (var n in nightOnly) n.ShowNow(night);

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) };
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = prev;
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }
            return Estimate(cam);
        }

        static (int renderers, int pairs) Estimate(Camera cam)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            int count = 0;
            var pairs = new HashSet<(int, int)>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                count++;
                var mf = r.GetComponent<MeshFilter>();
                int mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.GetInstanceID() : 0;
                int mat = r.sharedMaterial != null ? r.sharedMaterial.GetInstanceID() : 0;
                pairs.Add((mesh, mat));
            }
            return (count, pairs.Count);
        }
    }
}
