// M3.2 Greybox_M3 test scene builder (docs/specs/vehicles-m3.md section 2).
// Builds everything in code so nobody hand-places a road, a lane node or a car:
//   - Assets/Settings/Vehicles/Danfo.asset and TokunboSaloon.asset (spec 4 numbers)
//   - Assets/Prefabs/Vehicles/Danfo.prefab and TokunboSaloon.prefab (greybox, separate wheels)
//   - Assets/Scenes/Greybox_M3.unity: 300 x 140 m road loop (two lanes each way,
//     right-hand traffic), a bridge on the north side (deck 5.4 m, 6% ramps, piers
//     off the lanes) with a ground road passing under it, a four-way crossing on
//     the south side, a dead-end side road, a spur with a 3.2 m gate, a lay-by
//     with the two parked drivables, a car parked half in a lane, pedestrians
//     (one steps into the road on a timer), the lane graph, the bot spawner (12),
//     the player, the camera and the debug overlay.
// Every run deletes and rebuilds the scene. Change this file, not the scene.
//
// Menu:  Naija Streets > M3 > Build test scene
// Batch: Unity -batchmode -projectPath . -executeMethod NaijaLife.Vehicles.EditorTools.M3SceneBuilder.BuildBatch -logFile -
//
// UNVERIFIED: written by Claude 2026-11-06 without a Unity Editor.

using System.Collections.Generic;
using System.IO;
using NaijaLife.Player;
using NaijaLife.Verification;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NaijaLife.Vehicles.EditorTools
{
    public static class M3SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Greybox_M3.unity";
        const string DefDir = "Assets/Settings/Vehicles";
        const string PrefabDir = "Assets/Prefabs/Vehicles";
        const string MatDir = "Assets/Art/Materials/M3";
        const string MeshDir = "Assets/Art/Models/M3";
        const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        // Layout (metres). Loop corners at (+-HalfX, +-HalfZ).
        const float HalfX = 150f, HalfZ = 70f, CornerR = 25f;
        const float RoadHalfWidth = 7f;          // 2 lanes each way, 3.5 m lanes
        const float LaneOffset = 5.25f;          // bots drive the right (slow) lane
        const float PavementWidth = 3f, KerbHeight = 0.15f;
        const float DeckHeight = 5.4f, DeckHalfLen = 30f, RampLen = 90f; // 5.4 / 90 = 6%
        const float JunctionTrim = 10f;
        const float PlateTop = 0.03f;            // plates sit 1 cm above the road strips (no z-fighting)

        static readonly Vector3 Crossing = new Vector3(0f, 0f, -HalfZ);

        [MenuItem("Naija Streets/M3/Build test scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorUtility.DisplayDialog("Greybox_M3", Build(), "OK");
        }

        public static void BuildBatch()
        {
            try { Debug.Log("[M3Build] " + Build()); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError("[M3Build] FAILED: " + e); EditorApplication.Exit(1); }
        }

        public static string Build()
        {
            EnsureFolder(DefDir); EnsureFolder(PrefabDir); EnsureFolder(MatDir); EnsureFolder(MeshDir);
            EnsureLayers();

            var danfoDef = Definition("Danfo", d => d.ApplyDanfoPreset());
            var saloonDef = Definition("TokunboSaloon", d => d.ApplySaloonPreset());
            var danfoPrefab = VehiclePrefab("Danfo", danfoDef, new Color(1f, 0.8f, 0.05f), true);
            var saloonPrefab = VehiclePrefab("TokunboSaloon", saloonDef, new Color(0.62f, 0.64f, 0.68f), false);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("M3_Generated").transform;

            // ---- World ----
            var ground = Box("Ground", root, new Vector3(0f, -0.5f, 0f), new Vector3(520f, 1f, 420f), Mat("Grass", new Color(0.36f, 0.45f, 0.25f)), VehicleLayers.Ground);
            ground.isStatic = true;

            var roads = new GameObject("Roads").transform; roads.SetParent(root);
            var loop = LoopCentreline();
            var inner = Straight(Crossing, new Vector3(0f, 0f, HalfZ + 40f), 10f);
            var side = Straight(Crossing, new Vector3(0f, 0f, -HalfZ - 60f), 10f);
            var spur = Straight(new Vector3(-HalfX - RoadHalfWidth, 0f, 0f), new Vector3(-HalfX - 60f, 0f, 0f), 10f);

            var asphalt = Mat("Asphalt", new Color(0.18f, 0.18f, 0.19f));
            var pavement = Mat("Pavement", new Color(0.62f, 0.6f, 0.56f));
            RoadStrip("Road_Loop", roads, loop, true, true, asphalt);
            RoadStrip("Road_Inner", roads, inner, true, false, asphalt);
            RoadStrip("Road_Side", roads, side, true, false, asphalt);
            RoadStrip("Road_GateSpur", roads, spur, false, false, asphalt);
            Box("Junction", roads, Crossing + new Vector3(0f, PlateTop - 0.15f, 0f), new Vector3(2f * (RoadHalfWidth + PavementWidth), 0.3f, 2f * (RoadHalfWidth + PavementWidth)), asphalt, VehicleLayers.Ground).isStatic = true;
            Box("TurnCircle_Inner", roads, inner[inner.Count - 1] + new Vector3(0f, PlateTop - 0.15f, 6f), new Vector3(18f, 0.3f, 18f), asphalt, VehicleLayers.Ground).isStatic = true;
            Box("TurnCircle_Side", roads, side[side.Count - 1] + new Vector3(0f, PlateTop - 0.15f, -6f), new Vector3(18f, 0.3f, 18f), asphalt, VehicleLayers.Ground).isStatic = true;
            // Lay-by for the parked drivables: road surface, no kerb between it and the loop.
            Box("LayBy", roads, new Vector3(-95f, PlateTop - 0.15f, -HalfZ - RoadHalfWidth - 7f), new Vector3(34f, 0.3f, 14f), asphalt, VehicleLayers.Ground).isStatic = true;

            var paveRoot = new GameObject("Pavements").transform; paveRoot.SetParent(root);
            Pavements("Pave_Loop", paveRoot, loop, pavement, p => NearJunction(p) || InLayBy(p) || NearSpur(p));
            Pavements("Pave_Inner", paveRoot, inner, pavement, p => NearJunction(p) || UnderOrNearDeck(p));
            Pavements("Pave_Side", paveRoot, side, pavement, NearJunction);

            BridgeStructure(root);
            Gate(root);

            // ---- Lanes ----
            var graphGo = new GameObject("LaneGraph");
            graphGo.transform.SetParent(root);
            var graph = graphGo.AddComponent<LaneGraph>();
            BuildLanes(graphGo.transform, loop, inner, side);
            graph.Refresh();

            // ---- Vehicles ----
            var vehicles = new GameObject("Vehicles").transform; vehicles.SetParent(root);
            float layByZ = -HalfZ - RoadHalfWidth - 7f;
            var parkedDanfo = Place(danfoPrefab, vehicles, new Vector3(-104f, 0.05f, layByZ - 2f), 90f, "Danfo (parked)", true);
            Place(saloonPrefab, vehicles, new Vector3(-88f, 0.05f, layByZ - 2f), 90f, "TokunboSaloon (parked)", true);
            // Half in the slow lane of the east side, northbound (spec 2: bots must go round it).
            Place(saloonPrefab, vehicles, new Vector3(HalfX + LaneOffset + 0.9f, 0.05f, 0f), 0f, "Obstacle car (parked in lane)", false);

            var spawnerGo = new GameObject("TrafficSpawner");
            spawnerGo.transform.SetParent(root);
            var spawner = spawnerGo.AddComponent<TrafficSpawner>();
            spawner.graph = graph;
            spawner.prefabs = new[] { danfoPrefab.GetComponent<VehicleController>(), saloonPrefab.GetComponent<VehicleController>() };
            spawner.weights = new[] { 1f, 2f };
            spawner.maxAlive = 12;

            // ---- People ----
            var people = new GameObject("Pedestrians").transform; people.SetParent(root);
            var pedMat = Mat("Pedestrian", new Color(0.55f, 0.2f, 0.6f));
            float southPave = -HalfZ - RoadHalfWidth - PavementWidth * 0.5f;
            Pedestrian(people, new Vector3(-30f, KerbHeight, southPave), pedMat, false);
            Pedestrian(people, new Vector3(25f, KerbHeight, southPave), pedMat, false);
            Pedestrian(people, new Vector3(HalfX + RoadHalfWidth + 1.5f, KerbHeight, -30f), pedMat, false);
            Pedestrian(people, new Vector3(-HalfX - RoadHalfWidth - 1.5f, KerbHeight, 20f), pedMat, false);
            Pedestrian(people, new Vector3(-HalfX - RoadHalfWidth - 1.5f, KerbHeight, -25f), pedMat, false);
            Pedestrian(people, new Vector3(-45f, KerbHeight, southPave), pedMat, true); // steps into the road every 25 s

            // ---- Player, camera, light, overlay ----
            var play = new GameObject("M3_Playtest").transform;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f;
            sun.transform.SetParent(play); sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.shadows = LightShadows.Soft;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(play);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
            camGo.AddComponent<AudioListener>();

            GameObject player = null;
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Vector3 spawn = parkedDanfo.GetComponentInChildren<VehicleSeat>().doorPoint.position + new Vector3(1.2f, 0.1f, 0.6f);
            if (playerPrefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                player.transform.SetParent(play);
                player.transform.SetPositionAndRotation(spawn, Quaternion.Euler(0f, 200f, 0f));
                if (player.GetComponent<VehiclePlayerDriver>() == null) player.AddComponent<VehiclePlayerDriver>();
                if (player.GetComponent<DriveSession>() == null) player.AddComponent<DriveSession>();
                if (player.GetComponent<VehiclePromptBridge>() == null) player.AddComponent<VehiclePromptBridge>();
            }
            else Debug.LogWarning("[M3Build] Player prefab missing at " + PlayerPrefabPath);

            var rigGo = new GameObject("PlayerCamera");
            rigGo.transform.SetParent(play);
            var pc = rigGo.AddComponent<PlayerCamera>();
            if (player != null) pc.followTarget = player.transform;
            pc.obstacleLayers = ~(VehicleLayers.Bit(VehicleLayers.DrivenVehicle) | VehicleLayers.Bit(VehicleLayers.Pedestrian)
                                  | VehicleLayers.Bit(LayerMask.NameToLayer("CameraIgnore")) | VehicleLayers.Bit(LayerMask.NameToLayer("Ignore Raycast")));

            var overlay = new GameObject("VehicleDebugOverlay").AddComponent<VehicleDebugOverlay>();
            overlay.transform.SetParent(play);
            overlay.spawner = spawner;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Physics.SyncTransforms(); // edit-mode queries see the freshly built colliders
            int blockers = graph.FindBlockers().Count;
            return $"Built {ScenePath}: {graph.nodes.Count} lane nodes, lane blockers {blockers} (must be 0), " +
                   $"prefabs {AssetDatabase.GetAssetPath(danfoPrefab)}, {AssetDatabase.GetAssetPath(saloonPrefab)}" +
                   (player == null ? ", NO PLAYER (prefab missing)" : "");
        }

        // ------------------------------------------------------------------
        // Road geometry
        // ------------------------------------------------------------------

        /// <summary>Loop centreline starting and ending at the crossing, heading east first.</summary>
        static List<Vector3> LoopCentreline()
        {
            var corners = new[]
            {
                new Vector3(HalfX, 0f, -HalfZ), new Vector3(HalfX, 0f, HalfZ),
                new Vector3(-HalfX, 0f, HalfZ), new Vector3(-HalfX, 0f, -HalfZ)
            };
            var pts = new List<Vector3> { Crossing };
            Vector3 cur = Crossing;
            for (int c = 0; c < 4; c++)
            {
                Vector3 corner = corners[c], nextCorner = corners[(c + 1) % 4];
                Vector3 inDir = (corner - cur).normalized, outDir = (nextCorner - corner).normalized;
                Vector3 arcStart = corner - inDir * CornerR, arcEnd = corner + outDir * CornerR;
                AddStraight(pts, cur, arcStart, 10f);
                Vector3 centre = arcStart + outDir * CornerR;
                for (int a = 1; a <= 9; a++)
                {
                    float t = a / 9f * 90f * Mathf.Deg2Rad;
                    pts.Add(centre - outDir * CornerR * Mathf.Cos(t) + inDir * CornerR * Mathf.Sin(t));
                }
                cur = arcEnd;
            }
            AddStraight(pts, cur, Crossing, 10f);
            for (int i = 0; i < pts.Count; i++) { var p = pts[i]; p.y = HeightAt(p); pts[i] = p; }
            return pts;
        }

        /// <summary>Bridge on the north side: deck |x| <= 30 at 5.4 m, 6% ramps to |x| = 120.</summary>
        static float HeightAt(Vector3 p)
        {
            if (Mathf.Abs(p.z - HalfZ) > 1f) return 0f;
            float ax = Mathf.Abs(p.x);
            if (ax <= DeckHalfLen) return DeckHeight;
            if (ax >= DeckHalfLen + RampLen) return 0f;
            return DeckHeight * (DeckHalfLen + RampLen - ax) / RampLen;
        }

        static List<Vector3> Straight(Vector3 a, Vector3 b, float step)
        {
            var pts = new List<Vector3> { a };
            AddStraight(pts, a, b, step);
            return pts;
        }

        static void AddStraight(List<Vector3> pts, Vector3 a, Vector3 b, float step)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
            for (int i = 1; i <= n; i++) pts.Add(Vector3.Lerp(a, b, i / (float)n));
        }

        static Vector3 Tangent(List<Vector3> pts, int i, bool closed)
        {
            int n = pts.Count;
            Vector3 a = i > 0 ? pts[i - 1] : (closed ? pts[n - 2] : pts[i]);
            Vector3 b = i < n - 1 ? pts[i + 1] : (closed ? pts[1] : pts[i]);
            Vector3 t = b - a; t.y = 0f;
            return t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.forward;
        }

        static Vector3 RightOf(Vector3 t) => new Vector3(t.z, 0f, -t.x);

        static void RoadStrip(string name, Transform parent, List<Vector3> c, bool lanesTrimmedAtCrossing, bool closed, Material mat)
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
            float v = 0f;
            for (int i = 0; i < c.Count; i++)
            {
                Vector3 r = RightOf(Tangent(c, i, closed));
                Vector3 p = c[i] + Vector3.up * 0.02f;
                verts.Add(p - r * RoadHalfWidth); verts.Add(p + r * RoadHalfWidth);
                if (i > 0) v += Vector3.Distance(c[i], c[i - 1]);
                uvs.Add(new Vector2(0f, v / 7f)); uvs.Add(new Vector2(1f, v / 7f));
                if (i > 0)
                {
                    int b = verts.Count - 4;
                    // Upward-facing winding (left, right, next-left / next-left, right, next-right).
                    tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }
            }
            SaveMeshObject(name, parent, verts, tris, uvs, mat, VehicleLayers.Ground);
        }

        static void Pavements(string name, Transform parent, List<Vector3> c, Material mat, System.Func<Vector3, bool> skip)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var verts = new List<Vector3>(); var tris = new List<int>();
                bool open = false;
                for (int i = 0; i < c.Count; i++)
                {
                    if (skip(c[i])) { open = false; continue; }
                    Vector3 r = RightOf(Tangent(c, i, false)) * side;
                    Vector3 basePt = c[i];
                    Vector3 kerbBottom = basePt + r * RoadHalfWidth + Vector3.up * 0.02f;
                    Vector3 kerbTop = basePt + r * RoadHalfWidth + Vector3.up * KerbHeight;
                    Vector3 outer = basePt + r * (RoadHalfWidth + PavementWidth) + Vector3.up * KerbHeight;
                    bool raised = basePt.y > 0.3f;
                    Vector3 wallTop = outer + Vector3.up * 1.0f; // parapet on the bridge and ramps
                    verts.Add(kerbBottom); verts.Add(kerbTop); verts.Add(outer); verts.Add(raised ? wallTop : outer);
                    if (open)
                    {
                        int b = verts.Count - 8, n = verts.Count - 4;
                        // kerb face, pavement top, parapet: both windings so either side collides and renders.
                        Quad(tris, b + 0, b + 1, n + 0, n + 1);
                        Quad(tris, b + 1, b + 2, n + 1, n + 2);
                        Quad(tris, b + 2, b + 3, n + 2, n + 3);
                    }
                    open = true;
                }
                if (tris.Count > 0) SaveMeshObject(name + (side < 0 ? "_L" : "_R"), parent, verts, tris, null, mat, VehicleLayers.Ground);
            }
        }

        static void Quad(List<int> tris, int a0, int a1, int b0, int b1)
        {
            tris.AddRange(new[] { a0, a1, b0, b0, a1, b1 });
            tris.AddRange(new[] { a0, b0, a1, b0, b1, a1 });
        }

        static bool NearJunction(Vector3 p) => Flat(p - Crossing).magnitude < RoadHalfWidth + PavementWidth + 2f;
        static bool InLayBy(Vector3 p) => Mathf.Abs(p.z + HalfZ) < 1f && p.x > -114f && p.x < -76f;
        static bool NearSpur(Vector3 p) => Mathf.Abs(p.x + HalfX) < 1f && Mathf.Abs(p.z) < RoadHalfWidth + 1f;
        static bool UnderOrNearDeck(Vector3 p) => Mathf.Abs(p.z - HalfZ) < RoadHalfWidth + PavementWidth + 1f;
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static void BridgeStructure(Transform root)
        {
            var b = new GameObject("Bridge").transform; b.SetParent(root);
            var concrete = Mat("Concrete", new Color(0.55f, 0.55f, 0.55f));
            float deckWidth = 2f * (RoadHalfWidth + PavementWidth);
            // Slab under the deck surface: blocks the camera from below, never in a lane.
            Box("DeckSlab", b, new Vector3(0f, DeckHeight - 0.42f, HalfZ), new Vector3(2f * DeckHalfLen, 0.8f, deckWidth), concrete, 0).isStatic = true;
            // Round piers only outside the inner road (|x| <= 10 incl. pavements) and under the slab.
            float pierH = DeckHeight - 0.82f;
            foreach (float x in new[] { -26f, -14f, 14f, 26f })
                foreach (float z in new[] { HalfZ - 7f, HalfZ + 7f })
                {
                    var pier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pier.name = $"Pier_{x:0}_{z:0}";
                    pier.transform.SetParent(b);
                    pier.transform.position = new Vector3(x, pierH * 0.5f, z);
                    pier.transform.localScale = new Vector3(1.2f, pierH * 0.5f, 1.2f);
                    pier.GetComponent<Renderer>().sharedMaterial = concrete;
                    pier.isStatic = true;
                }
        }

        static void Gate(Transform root)
        {
            var g = new GameObject("Gate_3.2m").transform; g.SetParent(root);
            var m = Mat("GatePillar", new Color(0.85f, 0.85f, 0.8f));
            float x = -HalfX - 40f;
            // 3.2 m clear gap between the inner faces (spec 2).
            Box("Pillar_N", g, new Vector3(x, 1.25f, 1.6f + 0.4f), new Vector3(0.8f, 2.5f, 0.8f), m, 0).isStatic = true;
            Box("Pillar_S", g, new Vector3(x, 1.25f, -1.6f - 0.4f), new Vector3(0.8f, 2.5f, 0.8f), m, 0).isStatic = true;
            Box("Wall_N", g, new Vector3(x, 1.25f, 1.6f + 0.8f + 2.5f), new Vector3(0.4f, 2.5f, 5f), m, 0).isStatic = true;
            Box("Wall_S", g, new Vector3(x, 1.25f, -1.6f - 0.8f - 2.5f), new Vector3(0.4f, 2.5f, 5f), m, 0).isStatic = true;
        }

        // ------------------------------------------------------------------
        // Lanes
        // ------------------------------------------------------------------

        struct Arm { public LaneNode Out; public LaneNode In; public string Name; }

        static void BuildLanes(Transform parent, List<Vector3> loop, List<Vector3> inner, List<Vector3> side)
        {
            var arms = new List<Arm>();

            // Loop: forward lane leaves the crossing east and arrives from the west.
            var loopFwd = Lane(parent, "Loop_CCW", loop, true, true, true);
            var loopRev = Lane(parent, "Loop_CW", Reversed(loop), true, true, true);
            arms.Add(new Arm { Name = "E", Out = loopFwd[0], In = loopRev[loopRev.Count - 1] });
            arms.Add(new Arm { Name = "W", Out = loopRev[0], In = loopFwd[loopFwd.Count - 1] });

            // Dead-end roads with a turnaround at the far end.
            var deadEnds = new[] { inner, side };
            var deadEndNames = new[] { "Inner", "Side" };
            for (int d = 0; d < deadEnds.Length; d++)
            {
                var c = deadEnds[d]; string nm = deadEndNames[d];
                var outL = Lane(parent, nm + "_Out", c, true, false, false);
                var backL = Lane(parent, nm + "_Back", Reversed(c), false, true, false);
                Turnaround(parent, nm, outL[outL.Count - 1], backL[0], c[c.Count - 1], c[c.Count - 1] - c[c.Count - 2]);
                arms.Add(new Arm { Name = nm, Out = outL[0], In = backL[backL.Count - 1] });
            }

            // Four-way crossing: every incoming lane end links to every other arm's outgoing start.
            foreach (var a in arms)
            {
                a.In.crossingId = 1;
                a.In.speedLimitKmh = 25f;
                foreach (var b in arms)
                {
                    if (b.Name == a.Name) continue;
                    if (!a.In.next.Contains(b.Out)) a.In.next.Add(b.Out);
                    b.Out.crossingExit = true;
                }
            }
        }

        static List<Vector3> Reversed(List<Vector3> c) { var r = new List<Vector3>(c); r.Reverse(); return r; }

        /// <summary>
        /// Right-hand lane nodes along a centreline. Points within the junction
        /// trim distance of the crossing are dropped at a trimmed end.
        /// </summary>
        static List<LaneNode> Lane(Transform parent, string name, List<Vector3> c, bool trimStart, bool trimEnd, bool closed)
        {
            var go = new GameObject("Lane_" + name).transform; go.SetParent(parent);
            var nodes = new List<LaneNode>();
            for (int i = 0; i < c.Count; i++)
            {
                bool nearCrossing = Flat(c[i] - Crossing).magnitude < JunctionTrim;
                if (nearCrossing && ((trimStart && i < c.Count / 2) || (trimEnd && i >= c.Count / 2))) continue;
                Vector3 p = c[i] + RightOf(Tangent(c, i, closed)) * LaneOffset;
                p.y = c[i].y;
                var n = new GameObject($"{name}_{nodes.Count:000}").AddComponent<LaneNode>();
                n.transform.SetParent(go);
                n.transform.position = p;
                n.speedLimitKmh = c[i].y > 0.3f ? 40f : 50f;
                if (nodes.Count > 0) nodes[nodes.Count - 1].next.Add(n);
                nodes.Add(n);
            }
            return nodes;
        }

        static void Turnaround(Transform parent, string name, LaneNode from, LaneNode to, Vector3 end, Vector3 dir)
        {
            dir.y = 0f; dir.Normalize();
            Vector3 right = RightOf(dir);
            Vector3 centre = end + dir * 6f; // centre of the turning circle plate
            var go = new GameObject("Lane_" + name + "_Turn").transform; go.SetParent(parent);
            LaneNode prev = from;
            for (int a = 1; a <= 5; a++)
            {
                float t = a / 6f * Mathf.PI; // sweep from the right side, round the front, to the left side
                Vector3 p = centre + right * Mathf.Cos(t) * LaneOffset + dir * Mathf.Sin(t) * LaneOffset;
                var n = new GameObject($"{name}_Turn_{a}").AddComponent<LaneNode>();
                n.transform.SetParent(go); n.transform.position = p; n.speedLimitKmh = 15f;
                prev.next.Add(n); prev = n;
            }
            prev.next.Add(to);
        }

        // ------------------------------------------------------------------
        // Vehicles
        // ------------------------------------------------------------------

        static VehicleDefinition Definition(string name, System.Action<VehicleDefinition> preset)
        {
            string path = $"{DefDir}/{name}.asset";
            var def = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<VehicleDefinition>();
                preset(def);
                AssetDatabase.CreateAsset(def, path);
            }
            // Existing assets keep their tuned numbers: delete the asset to reset to the spec.
            return def;
        }

        static GameObject VehiclePrefab(string name, VehicleDefinition def, Color paint, bool danfo)
        {
            var root = new GameObject(name);
            int layer = VehicleLayers.Assignable(VehicleLayers.Vehicle);
            root.layer = layer;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = def.massKg;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var vc = root.AddComponent<VehicleController>();
            vc.definition = def;
            root.AddComponent<VehicleContactReporter>();
            root.AddComponent<VehicleDamage>();

            Vector3 s = def.bodySize;
            float bodyBottom = def.wheelRadius + 0.12f;
            var bodyMat = Mat(name + "_Paint", paint);
            var black = Mat("Black", new Color(0.05f, 0.05f, 0.05f));
            var glass = Mat("Glass", new Color(0.12f, 0.16f, 0.2f));

            // Body collider(s): a danfo is one box to the roof; a saloon is a lower box to
            // the beltline plus a narrower cabin box. Detail meshes have no colliders.
            float beltline = danfo ? s.y : s.y - 0.42f;
            Box("Body", root.transform, new Vector3(0f, bodyBottom + (beltline - bodyBottom) * 0.5f, 0f),
                new Vector3(s.x, beltline - bodyBottom, s.z), bodyMat, layer);

            if (danfo)
            {
                Visual("Stripe_L1", root.transform, new Vector3(-s.x * 0.5f - 0.01f, bodyBottom + 0.55f, 0f), new Vector3(0.02f, 0.08f, s.z * 0.98f), black);
                Visual("Stripe_L2", root.transform, new Vector3(-s.x * 0.5f - 0.01f, bodyBottom + 0.7f, 0f), new Vector3(0.02f, 0.08f, s.z * 0.98f), black);
                Visual("Stripe_R1", root.transform, new Vector3(s.x * 0.5f + 0.01f, bodyBottom + 0.55f, 0f), new Vector3(0.02f, 0.08f, s.z * 0.98f), black);
                Visual("Stripe_R2", root.transform, new Vector3(s.x * 0.5f + 0.01f, bodyBottom + 0.7f, 0f), new Vector3(0.02f, 0.08f, s.z * 0.98f), black);
                Visual("RoofRack", root.transform, new Vector3(0f, s.y + 0.06f, -0.2f), new Vector3(s.x * 0.85f, 0.08f, s.z * 0.7f), black);
                Visual("SideWindows_L", root.transform, new Vector3(-s.x * 0.5f - 0.01f, s.y - 0.45f, -0.2f), new Vector3(0.02f, 0.55f, s.z * 0.8f), glass);
                Visual("SideWindows_R", root.transform, new Vector3(s.x * 0.5f + 0.01f, s.y - 0.45f, -0.2f), new Vector3(0.02f, 0.55f, s.z * 0.8f), glass);
                Visual("Windscreen", root.transform, new Vector3(0f, s.y - 0.5f, s.z * 0.5f + 0.01f), new Vector3(s.x * 0.85f, 0.6f, 0.02f), glass);
                Label("RouteBoard", root.transform, new Vector3(0f, s.y - 0.12f, s.z * 0.5f + 0.03f), 0f, "OSHODI - OBALENDE", 0.06f, Color.black);
                Label("Sticker", root.transform, new Vector3(0f, bodyBottom + 0.95f, -s.z * 0.5f - 0.03f), 180f, "No Food For Lazy Man", 0.045f, Color.black);
            }
            else
            {
                // Saloon: glass cabin on the lower body (keeps its collider).
                Box("Cabin", root.transform, new Vector3(0f, s.y - 0.21f, -0.15f), new Vector3(s.x * 0.86f, 0.42f, s.z * 0.5f), glass, layer);
                Visual("Door_Odd", root.transform, new Vector3(s.x * 0.5f + 0.01f, bodyBottom + 0.3f, 0.5f), new Vector3(0.02f, 0.45f, 0.95f),
                    Mat("TokunboDoor", new Color(0.45f, 0.12f, 0.12f))); // one odd-colour door
            }

            // Wheels: colliders on children at the hub height + half the suspension travel.
            vc.frontLeft = WheelPair(root.transform, "FL", new Vector3(-def.track * 0.5f, 0f, def.wheelbase * 0.5f), def, black);
            vc.frontRight = WheelPair(root.transform, "FR", new Vector3(def.track * 0.5f, 0f, def.wheelbase * 0.5f), def, black);
            vc.rearLeft = WheelPair(root.transform, "RL", new Vector3(-def.track * 0.5f, 0f, -def.wheelbase * 0.5f), def, black);
            vc.rearRight = WheelPair(root.transform, "RR", new Vector3(def.track * 0.5f, 0f, -def.wheelbase * 0.5f), def, black);

            // Seat and doors. Driver on the left (Nigeria drives on the right).
            var seatGo = new GameObject("Seat");
            seatGo.transform.SetParent(root.transform, false);
            var seat = seatGo.AddComponent<VehicleSeat>();
            float seatZ = danfo ? s.z * 0.5f - 1.0f : 0.15f;
            seat.seatPoint = Point("SeatPoint", seatGo.transform, new Vector3(-0.4f, bodyBottom - 0.1f, seatZ));
            seat.doorPoint = Point("DoorPoint", seatGo.transform, new Vector3(-(s.x * 0.5f + 0.7f), 0f, seatZ));
            seat.exitPoints = new[]
            {
                Point("Exit_DriverSide", seatGo.transform, new Vector3(-(s.x * 0.5f + 0.8f), 0f, seatZ)),
                Point("Exit_PassengerSide", seatGo.transform, new Vector3(s.x * 0.5f + 0.8f, 0f, seatZ)),
                Point("Exit_Behind", seatGo.transform, new Vector3(0f, 0f, -(s.z * 0.5f + 1.2f)))
            };

            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static VehicleController.Wheel WheelPair(Transform root, string id, Vector3 hub, VehicleDefinition def, Material tyre)
        {
            var colGo = new GameObject("WheelCollider_" + id);
            colGo.transform.SetParent(root, false);
            colGo.transform.localPosition = hub + Vector3.up * (def.wheelRadius + def.suspensionDistance * 0.5f);
            colGo.layer = root.gameObject.layer;
            var wc = colGo.AddComponent<WheelCollider>();
            wc.radius = def.wheelRadius;
            wc.suspensionDistance = def.suspensionDistance;

            var vis = new GameObject("Wheel_" + id).transform;
            vis.SetParent(root, false);
            vis.localPosition = hub + Vector3.up * def.wheelRadius;
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.name = "Tyre";
            cyl.transform.SetParent(vis, false);
            cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            cyl.transform.localScale = new Vector3(def.wheelRadius * 2f, 0.11f, def.wheelRadius * 2f);
            cyl.GetComponent<Renderer>().sharedMaterial = tyre;
            return new VehicleController.Wheel { collider = wc, visual = vis };
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, string name, bool drivable)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.name = name;
            go.GetComponent<VehicleController>().playerDrivable = drivable;
            return go;
        }

        static void Pedestrian(Transform parent, Vector3 feet, Material mat, bool stepsOut)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = stepsOut ? "Pedestrian (steps out)" : "Pedestrian";
            go.transform.SetParent(parent);
            go.transform.position = feet + Vector3.up * 0.9f;
            go.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.layer = VehicleLayers.Assignable(VehicleLayers.Pedestrian);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var ped = go.AddComponent<M3TestPedestrian>();
            ped.stepsIntoRoad = stepsOut;
            // Step toward the road: the south pavement's road side is +z.
            ped.stepDirection = Vector3.forward;
        }

        // ------------------------------------------------------------------
        // Small helpers
        // ------------------------------------------------------------------

        static GameObject Box(string name, Transform parent, Vector3 centre, Vector3 size, Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.layer = layer < 0 ? 0 : layer;
            return go;
        }

        static void Visual(string name, Transform parent, Vector3 centre, Vector3 size, Material mat)
        {
            var go = Box(name, parent, centre, size, mat, parent.gameObject.layer);
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static void Label(string name, Transform parent, Vector3 pos, float yaw, string text, float size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text; tm.characterSize = size; tm.fontSize = 48; tm.anchor = TextAnchor.MiddleCenter; tm.color = color;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) { tm.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
        }

        static Transform Point(string name, Transform parent, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        static void SaveMeshObject(string name, Transform parent, List<Vector3> verts, List<int> tris, List<Vector2> uvs, Material mat, int layer)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            if (uvs != null && uvs.Count == verts.Count) mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = $"{MeshDir}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer < 0 ? 0 : layer;
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        static Material Mat(string name, Color c)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", c);
            m.color = c;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void EnsureLayers()
        {
            foreach (var name in new[] { VehicleLayers.VehicleName, VehicleLayers.DrivenVehicleName, VehicleLayers.PedestrianName, VehicleLayers.GroundName })
                if (LayerMask.NameToLayer(name) < 0)
                    throw new System.Exception($"Layer '{name}' missing. Pull main: ProjectSettings/TagManager.asset has it (layers 8 and 14 to 16).");
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
