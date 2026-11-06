// M2.1 Oshodi greybox builder (docs/specs/oshodi-greybox.md section 8).
//
// Builds two scenes from Assets/Data/Oshodi/oshodi-greybox-build.json:
//   Assets/Scenes/Districts/District_Oshodi_Core.unity   (the tile)
//   Assets/Scenes/Districts/Approach_South.unity         (road south, drop stop, roundabout)
// Every run deletes and rebuilds the root "Oshodi_Generated" (and
// "Oshodi_Playtest" in the core scene). Nothing outside those roots is touched.
//
// Positions come only from the JSON. To move something, change
// tools/oshodi/oshodi-layout.js, run `node tools/oshodi/oshodi-layout.js`,
// then rebuild here. Never move generated objects by hand: the next build
// throws the change away and SceneMatchesLayout fails.
//
// Menu:        Naija Streets > Build Oshodi greybox
// Batch mode:  Unity -batchmode -projectPath . \
//                -executeMethod NaijaLife.World.Editor.OshodiGreyboxBuilder.BuildAllBatch -logFile -
//
// "Oshodi_Playtest" (core scene only) holds a sun, a main camera, the Player
// prefab at the spawn and a PlayerCamera, so the scene can be played and
// screenshotted on its own for M2.1 and M2.2 QA (the sun carries TimeOfDay,
// the root a LampLightPool). When Bootstrap streams the tile in (M2.3), the
// streamer disables this root.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NaijaLife.World.Editor
{
    public static class OshodiGreyboxBuilder
    {
        public const string SceneDir = "Assets/Scenes/Districts";
        public const string CoreScenePath = SceneDir + "/District_Oshodi_Core.unity";
        public const string ApproachScenePath = SceneDir + "/Approach_South.unity";
        public const string GeneratedRoot = "Oshodi_Generated";
        public const string PlaytestRoot = "Oshodi_Playtest";
        const string MaterialDir = "Assets/Art/Materials/Greybox";
        const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        const string PlayerCameraType = "NaijaLife.Player.PlayerCamera, Assembly-CSharp";

        // Layer names the spec uses (section 8.3). EnsureLayers adds any missing one.
        public static readonly string[] Layers = { "Ground", "Building", "Prop", "CameraIgnore", "PlayerBlocker", "Marker" };

        [MenuItem("Naija Streets/Build Oshodi greybox")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = BuildAll();
            EditorUtility.DisplayDialog("Oshodi greybox", report, "OK");
        }

        public static void BuildAllBatch()
        {
            try
            {
                Debug.Log("[Oshodi] " + BuildAll());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[Oshodi] build failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Builds both scenes. Returns a one-line report.</summary>
        public static string BuildAll()
        {
            var data = GreyboxBuildFile.Load();
            EnsureLayers();
            var mats = EnsureMaterials(data.materials);
            EnsureFolder(SceneDir);

            int core = BuildScene(CoreScenePath, "core", data, mats, true);
            int approach = BuildScene(ApproachScenePath, "approach", data, mats, false);
            AddToBuildSettings(CoreScenePath);
            AddToBuildSettings(ApproachScenePath);
            AssetDatabase.SaveAssets();

            // Leave the core scene open, ready to play.
            EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);
            return $"built {core} objects in {CoreScenePath} and {approach} in {ApproachScenePath}";
        }

        static int BuildScene(string path, string sceneKey, GreyboxBuildFile data,
            Dictionary<string, Material> mats, bool withPlaytest)
        {
            Scene scene = File.Exists(path)
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (var go in scene.GetRootGameObjects())
                if (go.name == GeneratedRoot || go.name == PlaytestRoot)
                    UnityEngine.Object.DestroyImmediate(go);

            var root = new GameObject(GeneratedRoot);
            var groups = new Dictionary<string, Transform>();
            int count = 0;

            foreach (var it in data.items)
            {
                if (it.scene != sceneKey) continue;
                var go = MakePiece(it, mats);
                go.transform.SetParent(GroupTransform(root.transform, it.group, groups), false);
                go.transform.localPosition = new Vector3(it.x, it.y, it.z);
                go.transform.localRotation = Quaternion.Euler(it.rx, it.ry, it.rz);
                count++;
            }

            int markerLayer = LayerOrDefault("Marker");
            foreach (var m in data.markers)
            {
                if (m.scene != sceneKey) continue;
                var go = new GameObject(m.id) { layer = markerLayer };
                go.transform.SetParent(GroupTransform(root.transform, m.group, groups), false);
                go.transform.localPosition = new Vector3(m.x, m.y, m.z);
                go.transform.localRotation = Quaternion.Euler(0f, m.ry, 0f);
                count++;
            }

            if (withPlaytest) BuildPlaytest(data);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new IOException("Could not save " + path);
            return count;
        }

        static GameObject MakePiece(GreyboxItem it, Dictionary<string, Material> mats)
        {
            GameObject go;
            Vector3 scale;
            switch (it.shape)
            {
                case "cyl":
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    scale = new Vector3(it.sx, it.sy * 0.5f, it.sz); // Unity cylinder is 2 m tall
                    break;
                case "sphere":
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    scale = new Vector3(it.sx, it.sy, it.sz);
                    break;
                case "blocker": // collider only, never drawn
                    go = new GameObject();
                    go.AddComponent<BoxCollider>();
                    scale = new Vector3(it.sx, it.sy, it.sz);
                    break;
                default:
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    scale = new Vector3(it.sx, it.sy, it.sz);
                    break;
            }
            go.name = it.id;
            go.transform.localScale = scale;
            go.layer = LayerOrDefault(it.layer);

            if (it.shape != "blocker")
            {
                var existing = go.GetComponent<Collider>();
                switch (it.collider)
                {
                    case "none":
                        UnityEngine.Object.DestroyImmediate(existing);
                        break;
                    case "box":
                        if (!(existing is BoxCollider))
                        {
                            UnityEngine.Object.DestroyImmediate(existing);
                            go.AddComponent<BoxCollider>();
                        }
                        break;
                    case "meshConvex":
                        UnityEngine.Object.DestroyImmediate(existing);
                        var mc = go.AddComponent<MeshCollider>();
                        mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                        mc.convex = true;
                        break;
                    // "primitive": keep the shape's own collider
                }

                var mr = go.GetComponent<MeshRenderer>();
                if (mats.TryGetValue(it.mat ?? "", out var mat)) mr.sharedMaterial = mat;
                else Debug.LogWarning($"[Oshodi] {it.id}: no material '{it.mat}'");
                bool thin = it.sy <= 0.05f || it.mat == "paint_white" || it.layer == "CameraIgnore";
                if (thin) mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (it.isStatic)
            {
                var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
                if (it.layer == "Building" || it.layer == "Ground") flags |= StaticEditorFlags.OccluderStatic;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
            }
            return go;
        }

        static Transform GroupTransform(Transform root, string path, Dictionary<string, Transform> cache)
        {
            if (string.IsNullOrEmpty(path)) return root;
            if (cache.TryGetValue(path, out var t)) return t;
            int slash = path.LastIndexOf('/');
            var parent = slash < 0 ? root : GroupTransform(root, path.Substring(0, slash), cache);
            var go = new GameObject(slash < 0 ? path : path.Substring(slash + 1));
            go.transform.SetParent(parent, false);
            cache[path] = go.transform;
            return go.transform;
        }

        static void BuildPlaytest(GreyboxBuildFile data)
        {
            var root = new GameObject(PlaytestRoot);

            var sun = new GameObject("Sun");
            sun.transform.SetParent(root.transform, false);
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f); // TimeOfDay sets the real angle on the first frame
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            sun.AddComponent<TimeOfDay>();          // M2.2: live WAT, QA buttons 07:00 / 13:00 / 17:30 / 21:00
            root.AddComponent<LampLightPool>();     // M2.2: real lights on the nearest lamps at night

            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            cam.transform.SetParent(root.transform, false);
            var c = cam.AddComponent<Camera>();
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 600f;
            c.fieldOfView = 60f;
            cam.AddComponent<AudioListener>();

            GreyboxMarker spawn = null;
            foreach (var m in data.markers) if (m.id == "spawn") spawn = m;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab != null && spawn != null)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                player.transform.SetParent(root.transform, false);
                player.transform.position = new Vector3(spawn.x, spawn.y + 0.05f, spawn.z);
                player.transform.rotation = Quaternion.Euler(0f, spawn.ry, 0f);
                cam.transform.position = player.transform.position + new Vector3(0f, 3f, -5f);
                cam.transform.LookAt(player.transform.position + Vector3.up * 1.6f);
            }
            else Debug.LogWarning("[Oshodi] Player prefab or spawn marker missing; playtest root has no player.");

            var camType = Type.GetType(PlayerCameraType);
            if (camType != null)
            {
                var rig = new GameObject("PlayerCamera");
                rig.transform.SetParent(root.transform, false);
                rig.AddComponent(camType);
            }
            else Debug.LogWarning("[Oshodi] " + PlayerCameraType + " not found; add a PlayerCamera by hand.");
        }

        // ---------------------------------------------------------------- materials

        static Dictionary<string, Material> EnsureMaterials(GreyboxMaterial[] palette)
        {
            EnsureFolder(MaterialDir);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not found. Is URP the active pipeline (M0.1)?");
            var result = new Dictionary<string, Material>();
            foreach (var p in palette)
            {
                string path = $"{MaterialDir}/{p.key}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool isNew = mat == null;
                if (isNew) mat = new Material(shader);
                mat.shader = shader;
                var col = new Color(p.r, p.g, p.b, 1f);
                mat.SetColor("_BaseColor", col);
                mat.SetFloat("_Smoothness", p.key.StartsWith("glass") ? 0.9f : 0.2f);
                mat.enableInstancing = true;
                if (p.emission > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", col * p.emission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else mat.DisableKeyword("_EMISSION");
                if (isNew) AssetDatabase.CreateAsset(mat, path);
                else EditorUtility.SetDirty(mat);
                result[p.key] = mat;
            }
            return result;
        }

        // ---------------------------------------------------------------- project setup

        public static void EnsureLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            bool changed = false;
            foreach (var name in Layers)
            {
                if (LayerMask.NameToLayer(name) >= 0) continue;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    var el = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(el.stringValue)) { el.stringValue = name; changed = true; break; }
                }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        static int LayerOrDefault(string name)
        {
            int l = string.IsNullOrEmpty(name) ? 0 : LayerMask.NameToLayer(name);
            return l < 0 ? 0 : l;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == path) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
