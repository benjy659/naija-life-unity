// Naija Streets (Unity) - builds the M4 conductor test scene so Ted's team
// only has to press Play (docs/specs/jobs-m4.md section 11).
// Menu: Naija Streets > M4 > Build conductor test scene
// Batch: -executeMethod NaijaLife.JobsGlue.EditorTools.M4ConductorSceneBuilder.BuildBatch
// Makes Assets/Scenes/M4_ConductorTest.unity: a flat greybox park with 3 danfo
// bays laid out like the Oshodi layout (bus faces east, sliding door on a
// raised island to the south, work point at the door, aside points 2.4 m
// each way, a 6-spot queue), a bot conductor per bay, a loop route with one
// drop stop about 300 m out, the Player prefab with PlayerNeeds + PlayerJobs,
// the M1 camera and the placeholder job UI.
// When the real Oshodi scene (M2) is in, the same components go on its bays.
// UNVERIFIED (2026-11-06): written without an Editor; Ted's first run checks it.

using NaijaLife.Jobs;
using NaijaLife.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NaijaLife.JobsGlue.EditorTools
{
    public static class M4ConductorSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/M4_ConductorTest.unity";
        const string DefPath = "Assets/Settings/Jobs/Conductor.asset";
        const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        static readonly string[] RouteNames = { "UNILAG GATE", "IKEJA ALONG", "MILE 2" };

        [MenuItem("Naija Streets/M4/Build conductor test scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildBatch() { Build(); }

        static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            JobDefinition def = EnsureDefinition();
            Material grey = Mat("M4_Ground", new Color(0.32f, 0.33f, 0.35f));
            Material kerb = Mat("M4_Island", new Color(0.62f, 0.6f, 0.55f));
            Material yellow = Mat("M4_Danfo", new Color(0.98f, 0.78f, 0.1f));
            Material botMat = Mat("M4_Bot", new Color(0.15f, 0.45f, 0.25f));

            // Light and ground.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground"; ground.transform.localScale = new Vector3(120f, 1f, 60f);
            ground.transform.position = new Vector3(150f, 0f, 0f);
            ground.GetComponent<Renderer>().sharedMaterial = grey;

            // Three bays, 8 m apart north to south.
            for (int i = 0; i < 3; i++) BuildBay(i, def, kerb, yellow, botMat);

            // Player, camera, UI.
            GameObject player = SpawnPlayer(new Vector3(-30f, 0.1f, -6f));
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>().farClipPlane = 600f;
            camGo.AddComponent<AudioListener>();
            var rig = new GameObject("PlayerCamera").AddComponent<PlayerCamera>();
            if (player) rig.followTarget = player.transform;
            new GameObject("JobDebugUi").AddComponent<JobDebugUi>();

            EnsureFolder("Assets", "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[M4] Built " + ScenePath + ". Press Play, walk to a bot conductor on the island (south side of a bus) and press E.");
        }

        static void BuildBay(int i, JobDefinition def, Material kerb, Material yellow, Material botMat)
        {
            float z = -8f * i;
            var bayGo = new GameObject("Bay" + (i + 1));
            bayGo.transform.position = new Vector3(-24f, 0f, z);

            // Bus: faces east (+x); right side (south, -z) is the door side.
            GameObject bus = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bus.name = "Danfo_" + RouteNames[i].Replace(' ', '_');
            bus.transform.position = new Vector3(-24f, 1.25f, z);
            bus.transform.rotation = Quaternion.LookRotation(Vector3.right);
            bus.transform.localScale = Vector3.one;
            Object.DestroyImmediate(bus.GetComponent<BoxCollider>());
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body"; body.transform.SetParent(bus.transform, false);
            body.transform.localScale = new Vector3(2.0f, 2.3f, 5.4f);
            body.GetComponent<Renderer>().sharedMaterial = yellow;
            Object.DestroyImmediate(bus.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(bus.GetComponent<MeshFilter>());
            Transform doorSeat = Child(bus.transform, "DoorSeat", new Vector3(1.15f, -0.95f, 0.6f));

            // Island (the park's kerb), 2 m wide, south of the bus.
            GameObject island = GameObject.CreatePrimitive(PrimitiveType.Cube);
            island.name = "Island"; island.transform.position = new Vector3(-24f, 0.06f, z - 2.4f);
            island.transform.localScale = new Vector3(12f, 0.12f, 2f);
            island.GetComponent<Renderer>().sharedMaterial = kerb;

            Transform work = Child(bayGo.transform, "WorkPoint", new Vector3(0.6f, 0.12f, -2.2f));
            Transform west = Child(bayGo.transform, "AsideWest", new Vector3(-1.9f, 0.12f, -2.4f));
            Transform east = Child(bayGo.transform, "AsideEast", new Vector3(3.0f, 0.12f, -2.4f));
            Transform step = Child(bayGo.transform, "DoorStep", new Vector3(0.6f, 0.12f, -1.6f));
            work.rotation = Quaternion.LookRotation(Vector3.forward);

            // Queue behind aside-west, extending west.
            var qGo = new GameObject("Queue");
            qGo.transform.SetParent(bayGo.transform, false);
            qGo.transform.localPosition = new Vector3(-2.9f, 0.12f, -2.4f);
            PassengerQueue queue = qGo.AddComponent<PassengerQueue>();
            queue.queueDirection = Vector3.left;
            queue.door = step;

            // Bot conductor (greybox capsule).
            GameObject bot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bot.name = "BotConductor";
            Object.DestroyImmediate(bot.GetComponent<CapsuleCollider>());
            bot.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            bot.transform.position = work.position + Vector3.up * 0.9f;
            bot.GetComponent<Renderer>().sharedMaterial = botMat;
            BotWorker worker = bot.AddComponent<BotWorker>();
            worker.FaceWhileIdle(bus.transform);

            // Route: east along the bay lane to the stop (about 300 m), U-turn,
            // back west on the far lane, round to the bay.
            var routeGo = new GameObject("Route");
            routeGo.transform.SetParent(bayGo.transform, false);
            DanfoRoute route = routeGo.AddComponent<DanfoRoute>();
            Vector3 b = new Vector3(-24f, 1.25f, z);
            Vector3[] pts =
            {
                b,
                new Vector3(276f, 1.25f, z),          // drop stop
                new Vector3(340f, 1.25f, z),
                new Vector3(352f, 1.25f, z + 30f),
                new Vector3(-70f, 1.25f, z + 30f),
                new Vector3(-82f, 1.25f, z + 6f),
                new Vector3(-60f, 1.25f, z),
                b,
            };
            for (int k = 0; k < pts.Length; k++)
            {
                var wp = new GameObject(k == 1 ? "Stop" : "WP" + k);
                wp.transform.SetParent(routeGo.transform, false);
                wp.transform.position = pts[k];
            }

            // Slot + bay.
            JobSlot slot = bayGo.AddComponent<JobSlot>();
            slot.slotId = "oshodi-bay-" + (i + 1) + "-conductor";
            slot.definition = def;
            slot.workPoint = work; slot.asideWest = west; slot.asideEast = east;
            slot.bot = worker;
            ConductorBay bay = bayGo.AddComponent<ConductorBay>();
            bay.bus = bus.transform; bay.doorSeat = doorSeat; bay.doorStep = step;
            bay.route = route; bay.queue = queue; bay.routeName = RouteNames[i];
            bay.firstBotTripDelay = 5f + 20f * i;   // staggered departures
        }

        static GameObject SpawnPlayer(Vector3 at)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (!prefab) { Debug.LogError("[M4] Player prefab missing at " + PlayerPrefabPath); return null; }
            var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            p.transform.position = at;
            if (!p.GetComponent<PlayerNeeds>()) p.AddComponent<PlayerNeeds>();
            if (!p.GetComponent<PlayerJobs>()) p.AddComponent<PlayerJobs>();
            if (!p.GetComponent<ConductorShift>()) p.AddComponent<ConductorShift>();
            return p;
        }

        static JobDefinition EnsureDefinition()
        {
            var def = AssetDatabase.LoadAssetAtPath<JobDefinition>(DefPath);
            if (def) return def;
            EnsureFolder("Assets/Settings", "Jobs");
            def = ScriptableObject.CreateInstance<JobDefinition>();
            def.kind = JobKind.Conductor;
            AssetDatabase.CreateAsset(def, DefPath);
            return def;
        }

        static Material Mat(string name, Color c)
        {
            string path = "Assets/Settings/Jobs/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            EnsureFolder("Assets/Settings", "Jobs");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(lit) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Create a folder the AssetDatabase knows about (System.IO alone makes CreateAsset fail).</summary>
        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static Transform Child(Transform parent, string name, Vector3 local)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            return g.transform;
        }
    }
}
