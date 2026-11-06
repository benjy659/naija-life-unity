// M2.1 edit-mode checks for the Oshodi greybox (docs/specs/oshodi-greybox.md section 8.6).
// They open the generated scenes and test the colliders, so they check what the
// player will actually bump into, not just the plan. If the scenes do not exist
// yet, the fixture builds them first.
//
// Run: Window > General > Test Runner > EditMode > NaijaLife.World.Tests, or
//   Unity -batchmode -projectPath . -runTests -testPlatform EditMode \
//     -assemblyNames NaijaLife.World.Tests -testResults qa/M2.1/editmode-results.xml

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NaijaLife.World.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NaijaLife.World.Tests
{
    public class OshodiGreyboxTests
    {
        GreyboxBuildFile _data;

        // Things a walker or a vehicle would hit: everything except the walking
        // surface, paint/overhead dressing and markers.
        static int SolidMask => LayerMask.GetMask("Building", "Prop", "PlayerBlocker", "Default");
        static int GroundMask => LayerMask.GetMask("Ground");

        [OneTimeSetUp]
        public void Setup()
        {
            _data = GreyboxBuildFile.Load();
            if (!File.Exists(OshodiGreyboxBuilder.CoreScenePath) || !File.Exists(OshodiGreyboxBuilder.ApproachScenePath))
                OshodiGreyboxBuilder.BuildAll();
        }

        static void OpenCore()
        {
            EditorSceneManager.OpenScene(OshodiGreyboxBuilder.CoreScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
        }

        // The playtest player and camera are not world geometry.
        static bool IsPlaytest(Collider c) => c.transform.root.name == OshodiGreyboxBuilder.PlaytestRoot;

        static IEnumerable<Vector3> Samples(GreyboxLine l, float step)
        {
            var a = new Vector3(l.x0, l.y, l.z0);
            var b = new Vector3(l.x1, l.y, l.z1);
            float len = Vector3.Distance(a, b);
            for (float d = 0f; d <= len + 1e-3f; d += step) yield return Vector3.Lerp(a, b, d / len);
        }

        static List<string> Blockers(GreyboxLine[] lines, float radius)
        {
            var hits = new List<string>();
            foreach (var l in lines)
                foreach (var p in Samples(l, 1f))
                {
                    var bottom = p + Vector3.up * (radius + 0.3f);   // above kerbs and paint
                    var top = p + Vector3.up * 2.0f;                 // head height; overhead structures sit above 5.5 m
                    foreach (var c in Physics.OverlapCapsule(bottom, top, radius, SolidMask, QueryTriggerInteraction.Ignore))
                        if (!IsPlaytest(c)) hits.Add($"{l.id} at ({p.x:F1}, {p.z:F1}): {c.name}");
                }
            return hits.Distinct().ToList();
        }

        [Test]
        public void LanesAreClear()
        {
            OpenCore();
            var hits = Blockers(_data.checks.laneLines, 0.45f);
            Assert.IsEmpty(hits, "Something stands in a driving lane:\n" + string.Join("\n", hits.Take(40)));
        }

        [Test]
        public void PavementsAreWalkable()
        {
            OpenCore();
            var hits = Blockers(_data.checks.clearLines, 0.45f);
            Assert.IsEmpty(hits, "Something blocks a pavement clear zone:\n" + string.Join("\n", hits.Take(40)));
        }

        [Test]
        public void FootbridgeIsWalkable()
        {
            OpenCore();
            var w = _data.checks.footbridgeWalk;
            float prev = float.NaN, maxStep = 0f, maxY = float.MinValue;
            var problems = new List<string>();
            for (float x = w.x0; x <= w.x1 + 1e-3f; x += 0.1f)
            {
                var from = new Vector3(x, 12f, w.z0);
                if (!Physics.Raycast(from, Vector3.down, out var hit, 20f, GroundMask, QueryTriggerInteraction.Ignore))
                {
                    problems.Add($"no ground at x {x:F1}");
                    continue;
                }
                float y = hit.point.y;
                if (!float.IsNaN(prev)) maxStep = Mathf.Max(maxStep, Mathf.Abs(y - prev));
                prev = y;
                maxY = Mathf.Max(maxY, y);
                // body room above the walking surface (CharacterController 1.8 m tall, radius 0.35)
                var b = hit.point + Vector3.up * 0.45f;
                var t = hit.point + Vector3.up * 1.5f;
                foreach (var c in Physics.OverlapCapsule(b, t, 0.3f, SolidMask, QueryTriggerInteraction.Ignore))
                    if (!IsPlaytest(c)) problems.Add($"no headroom at x {x:F1} (y {y:F2}): {c.name}");
            }
            Assert.IsEmpty(problems.Distinct().ToList(), string.Join("\n", problems.Distinct().Take(40)));
            Assert.LessOrEqual(maxStep, _data.checks.stepMax, $"a step of {maxStep:F3} m along the footbridge walk");
            Assert.GreaterOrEqual(maxY, w.y - 0.05f, $"the walk never reaches the deck (max y {maxY:F2}, deck {w.y:F2})");
        }

        [Test]
        public void SceneMatchesLayout()
        {
            var problems = new List<string>();
            foreach (var pair in new[] { (path: OshodiGreyboxBuilder.CoreScenePath, key: "core"), (path: OshodiGreyboxBuilder.ApproachScenePath, key: "approach") })
            {
                string path = pair.path, key = pair.key;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var byName = new Dictionary<string, Transform>();
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == OshodiGreyboxBuilder.GeneratedRoot)
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                            byName[t.name] = t;

                foreach (var mergeKey in _data.items.Where(i => i.scene == key && !string.IsNullOrEmpty(i.merge)).Select(i => i.merge).Distinct())
                    if (!byName.ContainsKey(mergeKey)) problems.Add($"{key}: missing merged mesh {mergeKey}");
                foreach (var it in _data.items.Where(i => i.scene == key && string.IsNullOrEmpty(i.merge)))
                {
                    if (!byName.TryGetValue(it.id, out var t)) { problems.Add($"{key}: missing {it.id}"); continue; }
                    if (Vector3.Distance(t.position, new Vector3(it.x, it.y, it.z)) > 0.01f) problems.Add($"{key}: {it.id} moved to {t.position}");
                    if (Quaternion.Angle(t.rotation, Quaternion.Euler(it.rx, it.ry, it.rz)) > 0.5f) problems.Add($"{key}: {it.id} rotated");
                }
                foreach (var m in _data.markers.Where(q => q.scene == key))
                {
                    if (!byName.TryGetValue(m.id, out var t)) { problems.Add($"{key}: missing marker {m.id}"); continue; }
                    if (Vector3.Distance(t.position, new Vector3(m.x, m.y, m.z)) > 0.01f) problems.Add($"{key}: marker {m.id} moved");
                }
            }
            Assert.IsEmpty(problems, "Scene differs from the layout (rebuild with Naija Streets > Build Oshodi greybox):\n" + string.Join("\n", problems.Take(40)));
        }
    }
}
