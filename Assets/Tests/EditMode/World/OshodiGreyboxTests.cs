// OshodiGreyboxTests — M2.1 edit-mode tests (spec 8.6).
//   LanesAreClear, PavementsAreWalkable, FootbridgeIsWalkable, SceneMatchesLayout.
// These run in batch mode (and later in CI). Each test rebuilds the district
// scene from the layout first, so it always checks the current builder output.
// Note: the tests open District_Oshodi_Core.unity; they do not touch main.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NaijaLife.World.Tests
{
    public class OshodiGreyboxTests
    {
        private const float PosTol = 0.01f; // 1 cm (spec 8.6)
        private const float RotTol = 1f;    // 1 degree

        private OshodiGreyboxBuilder.LayoutData _layout;

        [SetUp]
        public void SetUp()
        {
            _layout = OshodiGreyboxBuilder.LoadLayout();
            OshodiGreyboxBuilder.BuildDistrictOshodiCore();
            Physics.SyncTransforms();
        }

        // Every JSON id exists in the scene at its position (within 1 cm) and
        // rotation (within 1 degree). Buildings are checked by centre + scale.
        [Test]
        public void SceneMatchesLayout()
        {
            var missing = new List<string>();
            var wrong = new List<string>();

            foreach (var b in _layout.buildings)
            {
                GameObject go = GameObject.Find(b.id);
                if (go == null) { missing.Add(b.id); continue; }
                Vector3 centre = OshodiGreyboxBuilder.RectCentre(b.r);
                Vector3 expected = new Vector3(centre.x, 0, centre.z);
                if (Vector3.Distance(go.transform.position, expected) > PosTol)
                    wrong.Add(b.id + " pos " + go.transform.position + " != " + expected);
                float expectedRy = OshodiGreyboxBuilder.FrontYaw(b.front);
                if (Mathf.Abs(Mathf.DeltaAngle(go.transform.eulerAngles.y, expectedRy)) > RotTol)
                    wrong.Add(b.id + " ry " + go.transform.eulerAngles.y + " != " + expectedRy);
                // World-space footprint (the collider bounds), not the local
                // scale: a 90/270 yaw swaps the local X/Z extents.
                var col = go.GetComponent<BoxCollider>();
                if (col == null) { missing.Add(b.id + " (no BoxCollider)"); continue; }
                Bounds bounds = col.bounds;
                Vector3 expectedMin = new Vector3(b.r.x0, 0, b.r.z0);
                Vector3 expectedMax = new Vector3(b.r.x1, b.h, b.r.z1);
                if (Vector3.Distance(bounds.min, expectedMin) > PosTol)
                    wrong.Add(b.id + " bounds.min " + bounds.min + " != " + expectedMin);
                if (Vector3.Distance(bounds.max, expectedMax) > PosTol)
                    wrong.Add(b.id + " bounds.max " + bounds.max + " != " + expectedMax);
            }

            foreach (var p in _layout.props)
            {
                if (p.type == "guard_rail") continue; // checked by from/to below
                GameObject go = GameObject.Find(p.id);
                if (go == null) { missing.Add(p.id); continue; }
                Vector3 expected = new Vector3(p.x, p.y, p.z);
                if (Vector3.Distance(go.transform.position, expected) > PosTol)
                    wrong.Add(p.id + " pos " + go.transform.position + " != " + expected);
            }

            foreach (var d in _layout.danfoPark.danfos)
            {
                GameObject go = GameObject.Find(d.id);
                if (go == null) { missing.Add(d.id); continue; }
                Vector3 expected = new Vector3(d.x, 0, d.z);
                if (Vector3.Distance(go.transform.position, expected) > PosTol)
                    wrong.Add(d.id + " pos " + go.transform.position + " != " + expected);
                if (Mathf.Abs(Mathf.DeltaAngle(go.transform.eulerAngles.y, d.ry)) > RotTol)
                    wrong.Add(d.id + " ry " + go.transform.eulerAngles.y + " != " + d.ry);
            }

            foreach (var b in _layout.okadaPark.bikes)
            {
                GameObject go = GameObject.Find(b.id);
                if (go == null) { missing.Add(b.id); continue; }
                Vector3 expected = new Vector3(b.x, 0, b.z);
                if (Vector3.Distance(go.transform.position, expected) > PosTol)
                    wrong.Add(b.id + " pos " + go.transform.position + " != " + expected);
            }

            foreach (var s in _layout.danfoPark.conductorSlots)
            {
                GameObject go = GameObject.Find(s.id);
                if (go == null) { missing.Add(s.id); continue; }
                Vector3 expected = new Vector3(s.x, 0.12f, s.z);
                if (Vector3.Distance(go.transform.position, expected) > PosTol)
                    wrong.Add(s.id + " pos " + go.transform.position + " != " + expected);
            }

            foreach (var s in _layout.okadaPark.riderSlots)
            {
                GameObject go = GameObject.Find(s.id);
                if (go == null) { missing.Add(s.id); continue; }
                Vector3 expected = new Vector3(s.x, 0, s.z);
                if (Vector3.Distance(go.transform.position, expected) > PosTol)
                    wrong.Add(s.id + " pos " + go.transform.position + " != " + expected);
            }

            // M4 bays: bay-N/workPoint, aside points, queue spots, door.
            foreach (var bay in _layout.danfoPark.bays)
            {
                GameObject bayGo = GameObject.Find(bay.id);
                if (bayGo == null) { missing.Add(bay.id); continue; }
                CheckChild(bayGo, "workPoint", new Vector3(bay.workPoint.x, 0.12f, bay.workPoint.z), missing, wrong);
                foreach (var ap in bay.asidePoints)
                    CheckChild(bayGo, ap.id, new Vector3(ap.x, 0.12f, ap.z), missing, wrong);
                for (int i = 0; i < bay.queue.Length; i++)
                    CheckChild(bayGo, "queue-" + (i + 1), new Vector3(bay.queue[i].x, 0.12f, bay.queue[i].z), missing, wrong);
                CheckChild(bayGo, "door", new Vector3(bay.door.x, 0.12f, bay.door.z), missing, wrong);
            }

            // Spawn + viewpoints.
            GameObject spawn = GameObject.Find("Spawn_Player");
            if (spawn == null) missing.Add("Spawn_Player");
            else if (Vector3.Distance(spawn.transform.position, new Vector3(_layout.spawn.x, _layout.spawn.y, _layout.spawn.z)) > PosTol)
                wrong.Add("Spawn_Player pos " + spawn.transform.position);
            foreach (var v in _layout.viewpoints)
            {
                GameObject go = GameObject.Find(v.id);
                if (go == null) { missing.Add(v.id); continue; }
                Vector3 expected = new Vector3(v.x, v.y, v.z);
                if (Vector3.Distance(go.transform.position, expected) > PosTol)
                    wrong.Add(v.id + " pos " + go.transform.position + " != " + expected);
                if (Mathf.Abs(Mathf.DeltaAngle(go.transform.eulerAngles.y, v.ry)) > RotTol)
                    wrong.Add(v.id + " ry " + go.transform.eulerAngles.y + " != " + v.ry);
            }

            // Flyover piers + footbridge piers.
            foreach (var p in _layout.flyover.piers)
            {
                GameObject go = GameObject.Find(p.id);
                if (go == null) { missing.Add(p.id); continue; }
                Vector3 expected = new Vector3(p.x, p.height * 0.5f, p.z);
                if (Vector3.Distance(go.transform.position, expected) > PosTol)
                    wrong.Add(p.id + " pos " + go.transform.position + " != " + expected);
            }
            foreach (var p in _layout.footbridge.piers)
            {
                GameObject go = GameObject.Find(p.id);
                if (go == null) { missing.Add(p.id); continue; }
                Vector3 expected = new Vector3(p.x, p.height * 0.5f, p.z);
                if (Vector3.Distance(go.transform.position, expected) > PosTol)
                    wrong.Add(p.id + " pos " + go.transform.position + " != " + expected);
            }

            Assert.IsEmpty(missing, "Missing GameObjects: " + string.Join(", ", missing));
            Assert.IsEmpty(wrong, "Wrong position/rotation/scale: " + string.Join("; ", wrong));
        }

        private void CheckChild(GameObject parent, string name, Vector3 expected, List<string> missing, List<string> wrong)
        {
            Transform t = parent.transform.Find(name);
            if (t == null) { missing.Add(parent.name + "/" + name); return; }
            if (Vector3.Distance(t.position, expected) > PosTol)
                wrong.Add(parent.name + "/" + name + " pos " + t.position + " != " + expected);
        }

        // For every lane rect, cast a 0.45 m capsule along its centre line every
        // 1 m at y 0.9; zero hits allowed except overhead structures above 5.5 m
        // (the capsule only spans y 0.45..1.35, so overhead things never hit).
        [Test]
        public void LanesAreClear()
        {
            var blockers = new List<string>();
            foreach (var lane in _layout.lanes)
            {
                var r = lane.r;
                float w = r.x1 - r.x0, d = r.z1 - r.z0;
                if (d >= w)
                {
                    float x = (r.x0 + r.x1) * 0.5f;
                    for (float z = r.z0 + 0.5f; z < r.z1; z += 1f)
                        CheckCapsule(new Vector3(x, 0.9f, z), lane.id, blockers);
                }
                else
                {
                    float z = (r.z0 + r.z1) * 0.5f;
                    for (float x = r.x0 + 0.5f; x < r.x1; x += 1f)
                        CheckCapsule(new Vector3(x, 0.9f, z), lane.id, blockers);
                }
            }
            Assert.IsEmpty(blockers, "Lane blockers: " + string.Join("; ", blockers));
        }

        // Same capsule cast along the middle of every pavement clear zone.
        [Test]
        public void PavementsAreWalkable()
        {
            var blockers = new List<string>();
            foreach (var p in _layout.pavements)
            {
                var r = p.clear;
                float w = r.x1 - r.x0, d = r.z1 - r.z0;
                if (d >= w)
                {
                    float x = (r.x0 + r.x1) * 0.5f;
                    for (float z = r.z0 + 0.5f; z < r.z1; z += 1f)
                        CheckCapsule(new Vector3(x, 0.9f, z), p.id, blockers);
                }
                else
                {
                    float z = (r.z0 + r.z1) * 0.5f;
                    for (float x = r.x0 + 0.5f; x < r.x1; x += 1f)
                        CheckCapsule(new Vector3(x, 0.9f, z), p.id, blockers);
                }
            }
            Assert.IsEmpty(blockers, "Pavement blockers: " + string.Join("; ", blockers));
        }

        private void CheckCapsule(Vector3 centre, string id, List<string> blockers)
        {
            Vector3 a = centre + Vector3.down * 0.45f;
            Vector3 b = centre + Vector3.up * 0.45f;
            Collider[] hits = Physics.OverlapCapsule(a, b, 0.45f);
            foreach (var h in hits)
            {
                // The ground itself is below the capsule (top y<=0.12); only
                // report things that actually stand in the way.
                if (h.bounds.min.y > 1.4f) continue;
                blockers.Add(id + " at " + centre + " hit by " + h.name + " (" + h.gameObject.layer + ")");
                return;
            }
        }

        // Drive a CharacterController from the west stair foot to the east stair
        // foot along z=-30 in 0.1 m steps with Move(); it must arrive, never
        // step more than 0.3 m, never fall.
        [Test]
        public void FootbridgeIsWalkable()
        {
            var fb = _layout.footbridge;
            float z = (fb.deck.z0 + fb.deck.z1) * 0.5f;
            float startX = fb.stairs[0].footprint.x0;
            float endX = fb.stairs[1].footprint.x1;
            var go = new GameObject("FootbridgeTestWalker");
            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.45f;
            cc.height = 1.8f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.08f;
            go.transform.position = new Vector3(startX, 0.12f, z);
            Physics.SyncTransforms();

            float maxStepY = 0f;
            float minY = float.MaxValue;
            int steps = 0;
            try
            {
                for (int i = 0; i < 800; i++)
                {
                    Vector3 before = go.transform.position;
                    cc.Move(new Vector3(0.1f, -0.05f, 0));
                    steps++;
                    float dy = go.transform.position.y - before.y;
                    if (dy > maxStepY) maxStepY = dy;
                    if (go.transform.position.y < minY) minY = go.transform.position.y;
                    if (go.transform.position.x >= endX - 0.2f) break;
                    // Stuck (no forward progress for a while) — fail fast.
                    if (i > 50 && go.transform.position.x < startX + 1f)
                        Assert.Fail("Footbridge walker stuck at " + go.transform.position + " after " + i + " steps");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            Assert.GreaterOrEqual(go.transform.position.x, endX - 0.5f,
                "Walker did not reach the east stair foot (x=" + go.transform.position.x + ", expected ~" + endX + ") after " + steps + " steps");
            Assert.LessOrEqual(maxStepY, 0.3f, "Walker stepped more than 0.3 m in one move (max " + maxStepY + ")");
            Assert.GreaterOrEqual(minY, 0f, "Walker fell below y=0 (min " + minY + ")");
        }
    }
}
