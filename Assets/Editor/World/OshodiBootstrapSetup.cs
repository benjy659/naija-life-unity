// M2.3 Bootstrap and Addressables setup (docs/specs/oshodi-greybox.md section 7).
//
// Run after "Build Oshodi greybox". It:
//   1. puts District_Oshodi_Core.unity in Addressables group "District_Oshodi"
//      with address "District_Oshodi_Core", and switches its Build Settings
//      entry off (a scene must not be both in Build Settings and Addressables);
//   2. makes Assets/Data/Oshodi/DistrictTile_Oshodi_Core.asset;
//   3. builds Assets/Scenes/Bootstrap.unity (first in Build Settings) with the
//      sun + TimeOfDay, main camera, player at the streaming test start
//      (z -790, saved inactive; the streamer switches it on once
//      Approach_South is loaded), PlayerCamera, DistrictStreamer,
//      FrameSpikeLogger and LampLightPool.
// Safe to run again: it rebuilds Bootstrap.unity and reuses the group, entry
// and tile asset.
//
// Menu:        Naija Streets > Set up Oshodi streaming (Bootstrap)
// Batch mode:  Unity -batchmode -projectPath . \
//                -executeMethod NaijaLife.World.Editor.OshodiBootstrapSetup.SetupBatch -logFile -

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NaijaLife.World.Editor
{
    public static class OshodiBootstrapSetup
    {
        public const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        public const string TileAssetPath = "Assets/Data/Oshodi/DistrictTile_Oshodi_Core.asset";
        public const string GroupName = "District_Oshodi";
        public const string CoreAddress = "District_Oshodi_Core";
        const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        const string PlayerCameraType = "NaijaLife.Player.PlayerCamera, Assembly-CSharp";

        [MenuItem("Naija Streets/Set up Oshodi streaming (Bootstrap)")]
        public static void SetupFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.DisplayDialog("Oshodi streaming", Setup(), "OK");
        }

        public static void SetupBatch()
        {
            try
            {
                Debug.Log("[Oshodi] " + Setup());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[Oshodi] Bootstrap setup failed: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static string Setup()
        {
            if (!File.Exists(OshodiGreyboxBuilder.CoreScenePath) || !File.Exists(OshodiGreyboxBuilder.ApproachScenePath))
                OshodiGreyboxBuilder.BuildAll();

            MakeAddressable();
            var tile = EnsureTile();
            BuildBootstrap(tile);
            AssetDatabase.SaveAssets();
            return $"Oshodi core is addressable as '{CoreAddress}' in group '{GroupName}'; {BootstrapScenePath} built. Press Play in Bootstrap and walk north.";
        }

        static void MakeAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(GroupName);
            if (group == null)
                group = settings.CreateGroup(GroupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            var guid = AssetDatabase.AssetPathToGUID(OshodiGreyboxBuilder.CoreScenePath);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = CoreAddress;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list)
                if (s.path == OshodiGreyboxBuilder.CoreScenePath) s.enabled = false;
            EditorBuildSettings.scenes = list.ToArray();
        }

        static DistrictTile EnsureTile()
        {
            var tile = AssetDatabase.LoadAssetAtPath<DistrictTile>(TileAssetPath);
            bool isNew = tile == null;
            if (isNew) tile = ScriptableObject.CreateInstance<DistrictTile>();
            tile.id = "Oshodi_Core";
            tile.sceneAddress = CoreAddress;
            tile.bounds = new Rect(-120f, -150f, 240f, 300f);   // spec section 2
            tile.wakeDistance = 600f;
            tile.sleepDistance = 700f;
            if (isNew) AssetDatabase.CreateAsset(tile, TileAssetPath);
            else EditorUtility.SetDirty(tile);
            return tile;
        }

        static void BuildBootstrap(DistrictTile tile)
        {
            var data = GreyboxBuildFile.Load();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            sun.AddComponent<TimeOfDay>();

            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = cam.AddComponent<Camera>();
            c.nearClipPlane = 0.1f;
            c.farClipPlane = 600f;
            c.fieldOfView = 60f;
            c.allowDynamicResolution = true;   // spec 6.4: dynamic resolution on every tier
            cam.AddComponent<AudioListener>();

            GreyboxMarker start = null;
            foreach (var m in data.markers) if (m.id == "streaming-test-start") start = m;
            if (start == null) throw new InvalidOperationException("Marker streaming-test-start missing from the build list.");

            GameObject player = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                player.transform.position = new Vector3(start.x, start.y + 0.05f, start.z);
                player.transform.rotation = Quaternion.Euler(0f, start.ry, 0f);
                player.SetActive(false);   // DistrictStreamer turns it on once the road is loaded
                cam.transform.position = player.transform.position + new Vector3(0f, 3f, -5f);
                cam.transform.LookAt(player.transform.position + Vector3.up * 1.6f);
            }
            else Debug.LogWarning("[Oshodi] " + PlayerPrefabPath + " missing; Bootstrap has no player.");

            var camType = Type.GetType(PlayerCameraType);
            if (camType != null)
            {
                var rig = new GameObject("PlayerCamera");
                var pc = rig.AddComponent(camType);
                // The player starts inactive, so a find-by-tag would miss it: set the target here.
                if (player != null)
                {
                    var so = new SerializedObject(pc);
                    var prop = so.FindProperty("followTarget");
                    if (prop != null) { prop.objectReferenceValue = player.transform; so.ApplyModifiedPropertiesWithoutUndo(); }
                }
            }
            else Debug.LogWarning("[Oshodi] " + PlayerCameraType + " not found; add a PlayerCamera by hand.");

            var world = new GameObject("World");
            var streamer = world.AddComponent<DistrictStreamer>();
            streamer.player = player != null ? player.transform : null;
            streamer.tiles = new[] { tile };
            streamer.alwaysLoadedScenes = new[] { Path.GetFileNameWithoutExtension(OshodiGreyboxBuilder.ApproachScenePath) };
            streamer.playtestRootName = OshodiGreyboxBuilder.PlaytestRoot;
            world.AddComponent<FrameSpikeLogger>();
            world.AddComponent<LampLightPool>();

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);

            // Bootstrap first, Approach_South enabled, core scene off (it comes from Addressables).
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == BootstrapScenePath);
            list.Insert(0, new EditorBuildSettingsScene(BootstrapScenePath, true));
            bool hasApproach = false;
            foreach (var s in list)
                if (s.path == OshodiGreyboxBuilder.ApproachScenePath) { s.enabled = true; hasApproach = true; }
            if (!hasApproach) list.Add(new EditorBuildSettingsScene(OshodiGreyboxBuilder.ApproachScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
