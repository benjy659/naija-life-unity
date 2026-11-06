// M3 self-check harness (docs/specs/vehicles-m3.md section 11). Runs in Greybox_M3
// play mode, drives the danfo with scripted input and measures what the owner
// rules say, then writes REPORT.md and screenshots. It is a helper for Ted's
// evidence, not a replacement: the clip and the phone/WebGL checks are still owed.
//
// Start it with Naija Streets > M3 > Run self-check (Editor) or batch:
//   Unity -projectPath . -executeMethod NaijaLife.Vehicles.EditorTools.M3VerifyRunner.RunBatch -logFile -
// (not -nographics: screenshots need a GPU). Output: qa/m3-selfcheck/<UTC time>/
//
// Checks (spec 11 line numbers):
//   1  Drive offer appears at the door, enter works, camera does not cut on enter
//   2  Around the loop and over the bridge: camera follows within 15 deg after
//      straights, never turns faster than 120 deg/s (no swing)
//   3  Reverse 4 s while steering: camera heading holds (< 3 deg)
//   4  Handbrake spin: lens settles behind within 3 s of driving straight
//   5  On the deck the danfo stays above 5 m (never falls through)
//   6/7 Exit works, player stands clear, walking controller back on
//   9  Traffic 120 s: no two vehicles overlap, no vehicle inside a pier or wall
//   10 Lane validator: 0 blockers
//
// UNVERIFIED: written by Claude 2026-11-06 without a Unity Editor.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NaijaLife.Vehicles;
using UnityEngine;

namespace NaijaLife.Verification
{
    public sealed class M3VerifyHarness : MonoBehaviour
    {
        public static event Action<int> Finished; // exit code: 0 = all PASS

        public string outDir;
        public float trafficSeconds = 120f;

        readonly StringBuilder _report = new StringBuilder();
        int _fails;
        DriveSession _session;
        VehiclePlayerDriver _driver;
        LaneGraph _graph;
        Camera _cam;

        void Start()
        {
            if (string.IsNullOrEmpty(outDir))
                outDir = Path.Combine(Application.dataPath, "..", "qa", "m3-selfcheck", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(outDir);
            StartCoroutine(Run());
        }

        void Line(bool pass, string line, string detail)
        {
            if (!pass) _fails++;
            string s = $"- {(pass ? "PASS" : "FAIL")} {line}: {detail}";
            _report.AppendLine(s);
            Debug.Log("[M3Verify] " + s);
        }

        void Info(string s) { _report.AppendLine("- INFO " + s); Debug.Log("[M3Verify] " + s); }

        void Shot(string name)
        {
            try { ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png")); } catch (Exception e) { Info("screenshot failed: " + e.Message); }
        }

        float CamYaw => _cam != null ? _cam.transform.eulerAngles.y : 0f;

        IEnumerator Run()
        {
            _report.AppendLine("# M3 self-check " + DateTime.UtcNow.ToString("u"));
            _report.AppendLine($"Screen {Screen.width}x{Screen.height}. Automated; the clip and phone checks are still owed.");
            _report.AppendLine();
            yield return new WaitForSeconds(2f);

            _session = FindFirstObjectByType<DriveSession>();
            _driver = _session != null ? _session.GetComponent<VehiclePlayerDriver>() : null;
            _graph = FindFirstObjectByType<LaneGraph>();
            _cam = Camera.main;
            if (_session == null || _driver == null || _cam == null) { Line(false, "setup", "DriveSession, VehiclePlayerDriver or Main Camera missing"); yield return Finish(); yield break; }

            // ---- 10: lane validator first (static scene) ----
            var blockers = _graph != null ? _graph.FindBlockers() : null;
            Line(blockers != null && blockers.Count == 0, "10 lane validator", blockers == null ? "no LaneGraph" : $"{blockers.Count} blockers {(blockers.Count > 0 ? blockers[0].ToString() : "")}");

            // ---- 1: offer + enter ----
            float t = 0f;
            while (_session.CurrentOffer == null && t < 3f) { t += Time.deltaTime; yield return null; }
            Line(_session.CurrentOffer != null, "1 drive offer", _session.CurrentOffer != null ? _session.CurrentOffer.name : "no offer within 3 s at spawn");
            Shot("01-offer");
            if (_session.CurrentOffer == null) { yield return Finish(); yield break; }
            float yaw0 = CamYaw, maxJump = 0f, prev = yaw0;
            _session.RequestInteract();
            t = 0f;
            while (_session.State != DriveState.Driving && t < 4f) { t += Time.deltaTime; yield return null; }
            for (float k = 0f; k < 0.6f; k += Time.deltaTime) { maxJump = Mathf.Max(maxJump, Mathf.Abs(Mathf.DeltaAngle(prev, CamYaw))); prev = CamYaw; yield return null; }
            Line(_session.State == DriveState.Driving, "1 enter", $"state {_session.State} after {t:0.0} s");
            Line(maxJump < 5f, "1 no cut on enter", $"largest one-frame camera turn {maxJump:0.0} deg");
            Shot("02-seated");
            if (_session.State != DriveState.Driving) { yield return Finish(); yield break; }
            var car = _session.CurrentVehicle;

            // ---- 2 + 5: drive the loop (pure pursuit on the lane graph) ----
            var route = RouteFrom(car.transform.position, 60);
            float maxRate = 0f, worstLagAfterStraight = 0f, minDeckY = float.MaxValue, driveT = 0f;
            int ri = 0; float straightFor = 0f; bool sawDeck = false;
            prev = CamYaw;
            while (driveT < 75f && ri < route.Count)
            {
                Vector3 target = route[ri];
                Vector3 to = target - car.transform.position; to.y = 0f;
                if (to.magnitude < 6f) { ri++; continue; }
                Vector3 local = car.transform.InverseTransformPoint(target);
                float steer = Mathf.Clamp(Mathf.Atan2(local.x, Mathf.Max(1f, local.z)) * Mathf.Rad2Deg / 25f, -1f, 1f);
                float speed = car.ForwardSpeed;
                float want = Mathf.Abs(steer) > 0.4f ? 7f : 11f;
                _driver.SetOverride(new DriveInput { Throttle = speed < want ? 0.7f : 0f, Brake = speed > want + 2f ? 0.4f : 0f, Steer = steer });

                float rate = Mathf.Abs(Mathf.DeltaAngle(prev, CamYaw)) / Mathf.Max(1e-4f, Time.deltaTime);
                prev = CamYaw;
                if (driveT > 1f) maxRate = Mathf.Max(maxRate, rate);
                straightFor = Mathf.Abs(steer) < 0.1f ? straightFor + Time.deltaTime : 0f;
                if (straightFor > 3f) worstLagAfterStraight = Mathf.Max(worstLagAfterStraight, Mathf.Abs(Mathf.DeltaAngle(CamYaw, car.transform.eulerAngles.y)));
                Vector3 p = car.transform.position;
                if (Mathf.Abs(p.x) < 25f && Mathf.Abs(p.z - 70f) < 6f) { sawDeck = true; minDeckY = Mathf.Min(minDeckY, p.y); }
                if (sawDeck && Mathf.Abs(p.x) < 5f) Shot("03-on-deck");
                driveT += Time.deltaTime;
                yield return null;
            }
            Info($"loop drive {driveT:0} s, reached route point {ri}/{route.Count}, at {car.transform.position}");
            Line(worstLagAfterStraight < 15f, "2 camera behind after straights", $"worst {worstLagAfterStraight:0.0} deg (limit 15)");
            Line(maxRate < 120f, "2 no swing", $"fastest camera turn {maxRate:0} deg/s (limit 120)");
            Line(sawDeck && minDeckY > 5f, "5 stays on the deck", sawDeck ? $"lowest y on the deck {minDeckY:0.00} m" : "never reached the deck");

            // ---- 3: reverse while steering ----
            yield return StopCar(car);
            float yawR = CamYaw;
            for (float k = 0f; k < 4f; k += Time.deltaTime) { _driver.SetOverride(new DriveInput { Brake = 1f, Steer = 1f }); yield return null; }
            Line(car.ForwardSpeed < -0.5f || car.Pedals.InReverse, "3 reverse engages", $"forward speed {car.ForwardSpeed:0.0} m/s");
            Line(Mathf.Abs(Mathf.DeltaAngle(yawR, CamYaw)) < 3f, "3 lens holds while reversing", $"camera turned {Mathf.Abs(Mathf.DeltaAngle(yawR, CamYaw)):0.0} deg");
            Shot("04-reversing");

            // ---- 4: handbrake spin then straight ----
            yield return StopCar(car);
            for (float k = 0f; k < 3f; k += Time.deltaTime) { _driver.SetOverride(new DriveInput { Throttle = 1f }); yield return null; }
            for (float k = 0f; k < 1.5f; k += Time.deltaTime) { _driver.SetOverride(new DriveInput { Throttle = 0.6f, Steer = 1f, Handbrake = true }); yield return null; }
            for (float k = 0f; k < 3f; k += Time.deltaTime) { _driver.SetOverride(new DriveInput { Throttle = 0.5f }); yield return null; }
            float spinLag = Mathf.Abs(Mathf.DeltaAngle(CamYaw, car.transform.eulerAngles.y));
            Line(spinLag < 20f, "4 lens settles after a spin", $"{spinLag:0.0} deg off the nose after 3 s straight (limit 20)");
            Shot("05-after-spin");

            // ---- 6/7: exit ----
            yield return StopCar(car);
            _driver.ClearOverride();
            bool exited = _session.TryExit();
            yield return null;
            var cc = _session.GetComponent<CharacterController>();
            bool clear = cc != null && !OverlapsOthers(cc);
            Line(exited && _session.State == DriveState.OnFoot, "6 exit", $"exited={exited} state={_session.State}");
            Line(clear, "6 player stands clear", clear ? "nothing overlaps the player" : "player capsule overlaps something");
            var pcType = Type.GetType("NaijaLife.Player.PlayerController, Assembly-CSharp");
            var pc = pcType != null ? _session.GetComponent(pcType) as Behaviour : null;
            Line(cc != null && cc.enabled && (pc == null || pc.enabled), "7 walking restored", $"CharacterController {(cc != null && cc.enabled)}, PlayerController {(pc == null ? "n/a" : pc.enabled.ToString())}");
            Shot("06-exited");

            // ---- 9: traffic, no pass-through ----
            yield return Traffic();

            yield return Finish();
        }

        bool OverlapsOthers(CharacterController cc)
        {
            Vector3 p = _session.transform.position;
            var hits = Physics.OverlapCapsule(p + Vector3.up * (cc.radius + 0.1f), p + Vector3.up * (cc.height - cc.radius),
                cc.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
                if (h != null && !h.transform.IsChildOf(_session.transform)) return true;
            return false;
        }

        IEnumerator StopCar(VehicleController car)
        {
            for (float k = 0f; k < 6f && Mathf.Abs(car.ForwardSpeed) > 0.2f; k += Time.deltaTime)
            {
                _driver.SetOverride(new DriveInput { Brake = car.ForwardSpeed > 0f ? 1f : 0f, Throttle = car.ForwardSpeed < 0f ? 0.6f : 0f });
                yield return null;
            }
            _driver.SetOverride(new DriveInput { Handbrake = true });
            yield return new WaitForSeconds(0.5f);
        }

        List<Vector3> RouteFrom(Vector3 p, int count)
        {
            var pts = new List<Vector3>();
            if (_graph == null) return pts;
            _graph.Refresh();
            // Start on the counter-clockwise loop lane (east along the south side).
            LaneNode best = null; float bestD = float.MaxValue;
            foreach (var n in _graph.nodes)
            {
                if (n == null || !n.name.StartsWith("Loop_CCW")) continue;
                float d = Vector3.Distance(n.transform.position, p);
                Vector3 dir = n.next.Count > 0 ? n.next[0].transform.position - n.transform.position : Vector3.zero;
                if (d < bestD && Vector3.Dot(dir, n.transform.position - p) > 0f) { best = n; bestD = d; }
            }
            for (var n = best; n != null && pts.Count < count; n = n.next.Count > 0 ? n.next[0] : null)
                pts.Add(n.transform.position);
            return pts;
        }

        IEnumerator Traffic()
        {
            var spawner = FindFirstObjectByType<TrafficSpawner>();
            if (spawner == null) { Line(false, "9 traffic", "no TrafficSpawner"); yield break; }
            int overlaps = 0, staticHits = 0, samples = 0, maxAlive = 0;
            string firstOverlap = null;
            var cols = new List<BoxCollider>();
            var buf = new Collider[16];
            for (float k = 0f; k < trafficSeconds; k += 0.25f)
            {
                yield return new WaitForSeconds(0.25f);
                samples++;
                maxAlive = Mathf.Max(maxAlive, spawner.AliveCount);
                cols.Clear();
                foreach (var v in VehicleController.All)
                    foreach (var b in v.GetComponentsInChildren<BoxCollider>()) cols.Add(b);
                for (int i = 0; i < cols.Count; i++)
                {
                    var a = cols[i];
                    for (int j = i + 1; j < cols.Count; j++)
                    {
                        var b = cols[j];
                        if (a.attachedRigidbody == b.attachedRigidbody) continue;
                        if (Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out float depth) && depth > 0.1f)
                        { overlaps++; if (firstOverlap == null) firstOverlap = $"{a.attachedRigidbody.name} / {b.attachedRigidbody.name} {depth:0.00} m"; }
                    }
                    // Static world (piers, gate, walls): any overlap is a pass-through.
                    var half = Vector3.Scale(a.size * 0.5f, a.transform.lossyScale) * 0.9f;
                    int n = Physics.OverlapBoxNonAlloc(a.bounds.center, half, buf, a.transform.rotation, ~VehicleLayers.Bit(VehicleLayers.Ground), QueryTriggerInteraction.Ignore);
                    for (int h = 0; h < n; h++)
                        if (buf[h] != null && buf[h].attachedRigidbody == null) { staticHits++; if (firstOverlap == null) firstOverlap = $"{a.attachedRigidbody?.name} in {buf[h].name}"; }
                }
                if (Mathf.Abs(k - 30f) < 0.01f) Shot("07-traffic-30s");
            }
            Info($"traffic {trafficSeconds:0} s, {samples} samples, up to {maxAlive} bots");
            Line(maxAlive >= 6, "9 traffic spawns", $"up to {maxAlive} bots (want 12, at least 6 within 2 min)");
            Line(overlaps == 0, "9 no vehicle passes through another", overlaps == 0 ? "0 overlaps" : $"{overlaps} overlap samples, first {firstOverlap}");
            Line(staticHits == 0, "9 no vehicle inside a pier, gate or wall", staticHits == 0 ? "0" : $"{staticHits} samples, first {firstOverlap}");
            int stuck = 0;
            foreach (var v in spawner.Alive) { var d = v != null ? v.GetComponent<TrafficDriver>() : null; if (d != null && d.Stuck != StuckState.Driving) stuck++; }
            Info($"{stuck} bots not in Driving state at the end (waiting, overtaking or rerouting)");
        }

        IEnumerator Finish()
        {
            yield return new WaitForEndOfFrame();
            _report.AppendLine();
            _report.AppendLine(_fails == 0 ? "RESULT: all automated checks PASS (still UNVERIFIED until the clip and phone checks)." : $"RESULT: {_fails} FAIL");
            File.WriteAllText(Path.Combine(outDir, "REPORT.md"), _report.ToString());
            Debug.Log("[M3Verify] wrote " + Path.Combine(outDir, "REPORT.md"));
            yield return new WaitForSeconds(1f); // let the last screenshot land
            Finished?.Invoke(_fails == 0 ? 0 : 1);
        }
    }
}
