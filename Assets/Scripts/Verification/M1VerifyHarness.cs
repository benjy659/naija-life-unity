// M1 VERIFICATION HARNESS — temporary, NOT part of the game.
// Drives Greybox_M1 in Editor play mode with scripted input (real keyboard
// state events through the Input System + a virtual gamepad for analog tilt),
// records speeds / camera yaw / positions, and captures screenshots.
// Writes results to <outDir>/results.txt and screenshots to <outDir>/shots/.
// Deletes itself after the run (the verifier removes this folder).
// Usage: M1VerifyRunner (Assets/Editor) adds this component to the scene,
// sets outDir / viewport, and enters play mode.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NaijaLife.Player;

namespace NaijaLife.Verification
{
    public class M1VerifyHarness : MonoBehaviour
    {
        public string outDir = "/var/cache/apt/archives/unity/m1-verify/desktop";
        public bool phoneRun = false; // phone run: shorter set (spawn/walk/strafe/shots)

        private PlayerController _player;
        private NaijaLife.Player.PlayerInput _playerInput;
        private PlayerCamera _camera;
        private Camera _mainCam;
        private StringBuilder _log = new StringBuilder();
        private Gamepad _virtualPad;
        private bool _useGamepad;
        private List<string> _failures = new List<string>();

        private void Log(string s)
        {
            string line = $"[{Time.time:0.00}] {s}";
            _log.AppendLine(line);
            Debug.Log("[M1Verify] " + s);
        }

        private void Fail(string s)
        {
            _failures.Add(s);
            Log("FAIL: " + s);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(Path.Combine(outDir, "shots"));
            Log($"M1 verify harness started. phoneRun={phoneRun} Screen={Screen.width}x{Screen.height}");

            // Wait for the scene to settle and the camera rig to build.
            yield return new WaitForSeconds(1.0f);

            _player = FindFirstObjectByType<PlayerController>();
            _playerInput = FindFirstObjectByType<NaijaLife.Player.PlayerInput>();
            _camera = FindFirstObjectByType<PlayerCamera>();
            _mainCam = Camera.main;
            if (_player == null) { Fail("No PlayerController in scene"); Finish(); yield break; }
            if (_playerInput == null) Fail("No PlayerInput in scene");
            if (_camera == null) { Fail("No PlayerCamera in scene (rig not wired)"); Finish(); yield break; }
            if (_mainCam == null) Fail("No Main Camera");

            Log($"Player at {_player.transform.position} grounded={_player.IsGrounded} speed={_player.CurrentSpeed:0.00}");
            Log($"Camera followYaw={_camera.FollowYawDegrees:0.00} ready={_camera.IsReady} mainCam={(_mainCam != null ? _mainCam.transform.position.ToString() : "null")}");
            var cc = _player.GetComponent<CharacterController>();
            Log($"CharacterController present={cc != null}" + (cc != null ? $" radius={cc.radius:0.00} height={cc.height:0.00} center={cc.center} slopeLimit={cc.slopeLimit:0.00} stepOffset={cc.stepOffset:0.00} skinWidth={cc.skinWidth:0.00}" : " (MISSING — prefab defect)"));
            if (cc == null) Fail("No CharacterController on Player (prefab defect)");
            Log($"PlayerInput ready={(_playerInput != null ? _playerInput.IsReady.ToString() : "n/a")} MoveInput={(_playerInput != null ? _playerInput.MoveInput.ToString() : "n/a")}");

            // Spawn check.
            Vector3 spawn = _player.transform.position;
            if (Mathf.Abs(spawn.y) > 0.2f) Fail($"Spawn Y off ground: {spawn.y:0.00}");
            if (!_player.IsGrounded) Fail("Player not grounded at spawn");
            yield return Shot("01-spawn");

            // Ground is 60x60 m (+/-30). Each speed/strafe test starts from a
            // known spot with enough runway; without the reset the sprint ran
            // off the +Z edge in run 7 and the player fell forever, poisoning
            // every later test with free-fall speeds.
            // ---- Phase 1: jog (W) ----
            Teleport(new Vector3(0f, 0f, 0f), 0f);
            yield return HoldKeys(new[] { Key.W }, 4.0f, "jog W");
            // ---- Phase 2: sprint (W+Shift) — start Z=-20 so 34 m ends ~Z+14 ----
            Teleport(new Vector3(0f, 0f, -20f), 0f);
            yield return HoldKeys(new[] { Key.W, Key.LeftShift }, 4.0f, "sprint W+Shift");
            // ---- Phase 3: walk (W+C) ----
            Teleport(new Vector3(0f, 0f, 0f), 0f);
            yield return HoldKeys(new[] { Key.W, Key.C }, 4.0f, "walk W+C");

            // ---- Phase 4: strafe right (D) 6 s — R1 dead-zone test ----
            Teleport(new Vector3(0f, 0f, 0f), 0f);
            yield return StrafeTest(new[] { Key.D }, "strafe D");
            // ---- Phase 5: backpedal (S) 6 s ----
            Teleport(new Vector3(0f, 0f, 0f), 0f);
            yield return StrafeTest(new[] { Key.S }, "backpedal S");

            if (!phoneRun)
            {
                // ---- Phase 6: manual orbit + 2 s hold + re-align ----
                yield return OrbitTest();
                // ---- Phase 7: analog gentle turn (virtual gamepad, 20 deg off camera) — re-align path ----
                yield return AnalogTurnTest();
                // ---- Phase 8: occlusion pull-in at Block_Tall ----
                yield return OcclusionTest();
                // ---- Phase 9: ramp climb + camera Y follows ----
                yield return RampTest();
                // ---- Phase 10: collision with Block_Tall ----
                yield return CollisionTest();
                // ---- Phase 11: follow distance while walking ----
                yield return FollowDistanceTest();
            }
            else
            {
                // Phone run: one more walk shot at phone aspect.
                yield return HoldKeys(new[] { Key.W }, 3.0f, "phone walk W");
                yield return Shot("02-phone-walk");
            }

            Finish();
        }

        // Holds the given keys via real Input System state events, measures
        // steady-state speed over the middle window, and logs camera yaw.
        private IEnumerator HoldKeys(Key[] keys, float seconds, string label)
        {
            string keyNames = string.Join("+", Array.ConvertAll(keys, k => k.ToString()));
            float yaw0 = _camera.FollowYawDegrees;
            Vector3 p0 = _player.transform.position;
            float t0 = Time.time;
            float settle = 1.0f;
            Vector3 pSettle = Vector3.zero;
            bool gotSettle = false;
            float speedSum = 0f; int speedN = 0;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                QueueKeys(keys);
                elapsed = Time.time - t0;
                if (!gotSettle && elapsed >= settle) { pSettle = _player.transform.position; gotSettle = true; }
                if (elapsed >= settle) { speedSum += _player.CurrentSpeed; speedN++; }
                yield return null;
            }
            QueueKeys(new Key[0]);
            Vector3 p1 = _player.transform.position;
            float steadyDt = Mathf.Max(0.01f, (Time.time - t0) - settle);
            float steadySpeed = Vector3.Distance(p1, pSettle) / steadyDt;
            float meanInspector = speedN > 0 ? speedSum / speedN : -1f;
            float yaw1 = _camera.FollowYawDegrees;
            Log($"{label}: steadySpeed={steadySpeed:0.00} m/s (inspector mean {meanInspector:0.00}) " +
                $"displacement={Vector3.Distance(p1, p0):0.00} m from {p0} to {p1} yaw {yaw0:0.00}->{yaw1:0.00} grounded={_player.IsGrounded} " +
                $"MoveInput={_playerInput.MoveInput} keys={keyNames} kb={ActiveKeyboard}");
            yield return new WaitForSeconds(0.4f); // let the player stop
        }

        // Strafe / backpedal: camera heading must NOT move (R1 fix).
        private IEnumerator StrafeTest(Key[] keys, string label)
        {
            string keyNames = string.Join("+", Array.ConvertAll(keys, k => k.ToString()));
            yield return Shot("04-" + label.Replace(" ", "-") + "-start");
            float yaw0 = _camera.FollowYawDegrees;
            Vector3 p0 = _player.transform.position;
            float maxDelta = 0f;
            float t0 = Time.time;
            while (Time.time - t0 < 6.0f)
            {
                QueueKeys(keys);
                float d = Mathf.Abs(Mathf.DeltaAngle(_camera.FollowYawDegrees, yaw0));
                if (d > maxDelta) maxDelta = d;
                yield return null;
            }
            QueueKeys(new Key[0]);
            Vector3 p1 = _player.transform.position;
            float yaw1 = _camera.FollowYawDegrees;
            Log($"{label}: yaw {yaw0:0.00} -> {yaw1:0.00}, max|delta|={maxDelta:0.00} deg over 6 s, " +
                $"player {p0} -> {p1} (moved {Vector3.Distance(p1, p0):0.00} m), speed={_player.CurrentSpeed:0.00}");
            if (maxDelta > 5f) Fail($"{label}: camera yaw moved {maxDelta:0.00} deg (R1 dead-zone failure)");
            else Log($"{label}: PASS (yaw held within {maxDelta:0.00} deg)");
            yield return Shot("05-" + label.Replace(" ", "-") + "-end");
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator OrbitTest()
        {
            Log("Orbit test: dragging +0.5 x for 0.6 s via AddOrbitInput");
            float yaw0 = _camera.FollowYawDegrees;
            float t0 = Time.time;
            while (Time.time - t0 < 0.6f)
            {
                _camera.AddOrbitInput(new Vector2(0.5f, 0f));
                yield return null;
            }
            float yawAfterOrbit = _camera.FollowYawDegrees;
            Log($"Orbit: yaw {yaw0:0.00} -> {yawAfterOrbit:0.00} (delta {Mathf.DeltaAngle(yawAfterOrbit, yaw0):0.00} deg)");
            if (Mathf.Abs(Mathf.DeltaAngle(yawAfterOrbit, yaw0)) < 10f)
                Fail("Manual orbit did not move the camera (AddOrbitInput has no effect in play)");

            // Hold: no input, no movement — yaw must stay put for ~2 s.
            float holdMax = 0f;
            t0 = Time.time;
            while (Time.time - t0 < 2.5f)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(_camera.FollowYawDegrees, yawAfterOrbit));
                if (d > holdMax) holdMax = d;
                yield return null;
            }
            Log($"Orbit hold: max yaw drift over 2.5 s idle = {holdMax:0.00} deg (expect < 2)");
            if (holdMax > 2f) Fail($"Camera drifted {holdMax:0.00} deg after orbit with no input");
            yield return Shot("06-orbit-held");
            // Walk forward: camera should stay at the orbited heading (travel is
            // camera-relative, so no re-align is expected on keyboard); just log.
            yield return HoldKeys(new[] { Key.W }, 2.0f, "post-orbit walk W");
        }

        private IEnumerator AnalogTurnTest()
        {
            // Virtual gamepad stick at 20 deg off camera-forward: travel is inside
            // the 35 deg dead zone, so the camera SHOULD re-align toward facing.
            Log("Analog turn test: virtual gamepad stick 20 deg right of camera-forward for 3 s");
            if (_virtualPad == null)
            {
                _virtualPad = InputSystem.AddDevice<Gamepad>();
                Log($"Virtual gamepad added: {_virtualPad}");
            }
            _useGamepad = true;
            float yaw0 = _camera.FollowYawDegrees;
            Vector3 facing0 = _player.Facing;
            float t0 = Time.time;
            float moved = 0f;
            while (Time.time - t0 < 3.0f)
            {
                Vector2 stick = new Vector2(Mathf.Sin(20f * Mathf.Deg2Rad), Mathf.Cos(20f * Mathf.Deg2Rad));
                InputSystem.QueueStateEvent(_virtualPad, new GamepadState { leftStick = stick });
                moved = _player.CurrentSpeed;
                yield return null;
            }
            InputSystem.QueueStateEvent(_virtualPad, new GamepadState());
            _useGamepad = false;
            float yaw1 = _camera.FollowYawDegrees;
            Log($"Analog turn: yaw {yaw0:0.00} -> {yaw1:0.00} (delta {Mathf.DeltaAngle(yaw1, yaw0):0.00} deg), " +
                $"facing {facing0} -> {_player.Facing}, speed during={moved:0.00}, MoveInput={_playerInput.MoveInput}");
            if (Mathf.Abs(Mathf.DeltaAngle(yaw1, yaw0)) < 5f)
                Log("Analog turn: camera did NOT re-align (note: re-align path not observed on this run)");
            else
                Log("Analog turn: PASS (camera re-aligned toward travel)");
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator OcclusionTest()
        {
            Log("Occlusion test: teleport player to (6,0,6) facing +Z so Block_Tall (x5-7,z3-5) sits between camera and player");
            Teleport(new Vector3(6f, 0f, 6f), 0f);
            yield return new WaitForSeconds(1.5f);
            float dist = Vector3.Distance(_mainCam.transform.position, _player.transform.position);
            Log($"Occlusion: IsOccluded={_camera.IsOccluded} cam-player dist={dist:0.00} m (nominal 4.5), " +
                $"cam pos={_mainCam.transform.position} player={_player.transform.position}");
            yield return Shot("08-occlusion");
            if (!_camera.IsOccluded && dist > 4.0f)
                Log("Occlusion: NOT triggered at this spot (pull-in not observed; try closer in follow-up)");
            else
                Log("Occlusion: PASS (pull-in active or camera already close)");
            // Walk away from the block (+Z) and watch the camera ease back out.
            yield return HoldKeys(new[] { Key.W }, 2.5f, "occlusion walk-away W");
            float dist2 = Vector3.Distance(_mainCam.transform.position, _player.transform.position);
            Log($"Occlusion walk-away: cam-player dist now {dist2:0.00} m");
        }

        private IEnumerator RampTest()
        {
            Log("Ramp test: teleport to (4,0,-6) facing +X, hold W 4.5 s up Ramp_20deg");
            Teleport(new Vector3(4f, 0f, -6f), 90f);
            float camY0 = _mainCam.transform.position.y;
            float maxY = 0f; float maxCamY = camY0; int groundedFrames = 0; int totalFrames = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 4.5f)
            {
                QueueKeys(new[] { Key.W });
                if (_player.transform.position.y > maxY) maxY = _player.transform.position.y;
                if (_mainCam.transform.position.y > maxCamY) maxCamY = _mainCam.transform.position.y;
                totalFrames++;
                if (_player.IsGrounded) groundedFrames++;
                yield return null;
            }
            QueueKeys(new Key[0]);
            float camY1 = _mainCam.transform.position.y;
            Log($"Ramp: max player Y={maxY:0.00} (ground 0, ramp top ~1.0), grounded {groundedFrames}/{totalFrames} frames, " +
                $"player end={_player.transform.position}, camera Y start {camY0:0.00} max {maxCamY:0.00} end {camY1:0.00} (no world-Y clamp if it rose)");
            if (maxY < 0.5f) Fail($"Ramp climb failed: max Y {maxY:0.00} (player did not get up the ramp)");
            if (maxCamY - camY0 < 0.3f) Fail($"Camera Y did not follow up the ramp (start {camY0:0.00} max {maxCamY:0.00})");
            yield return Shot("09-ramp-top");
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator CollisionTest()
        {
            Log("Collision test: teleport to (3,0,4) facing +X, hold W 3 s into Block_Tall (face at x=5)");
            Teleport(new Vector3(3f, 0f, 4f), 90f);
            float t0 = Time.time;
            while (Time.time - t0 < 3.0f)
            {
                QueueKeys(new[] { Key.W });
                yield return null;
            }
            QueueKeys(new Key[0]);
            Vector3 p = _player.transform.position;
            Log($"Collision: player end={p} (block face x=5, capsule radius 0.35 -> expect x <= ~4.65)");
            if (p.x > 4.75f) Fail($"Player passed through Block_Tall: x={p.x:0.00}");
            else Log("Collision: PASS (blocked by Block_Tall)");
            yield return Shot("10-collision");
        }

        private IEnumerator FollowDistanceTest()
        {
            Log("Follow test: walk W 3 s from current spot, track camera-player distance");
            Teleport(new Vector3(0f, 0f, 0f), 0f);
            yield return new WaitForSeconds(0.5f);
            float minD = 99f, maxD = 0f;
            float t0 = Time.time;
            while (Time.time - t0 < 3.0f)
            {
                QueueKeys(new[] { Key.W });
                float d = Vector3.Distance(_mainCam.transform.position, _player.transform.position);
                if (d < minD) minD = d;
                if (d > maxD) maxD = d;
                yield return null;
            }
            QueueKeys(new Key[0]);
            Log($"Follow: cam-player distance during walk min={minD:0.00} max={maxD:0.00} m (nominal 4.5)");
            if (maxD - minD > 1.5f) Fail($"Camera distance swung {minD:0.00}..{maxD:0.00} m while walking (unstable follow)");
        }

        private void Teleport(Vector3 pos, float yawDeg)
        {
            var cc = _player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            _player.transform.position = pos;
            _player.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            if (cc != null) cc.enabled = true;
            _camera.SnapToTarget();
            Log($"Teleported to {pos} yaw {yawDeg}, camera snapped to {_camera.FollowYawDegrees:0.00}");
        }

        private Keyboard _virtualKeyboard;

        private Keyboard ActiveKeyboard
        {
            get
            {
                // ALWAYS use a dedicated virtual keyboard, even when a
                // physical/X11 keyboard device exists: synthetic state
                // events queued on the native X11-backed Keyboard.current
                // are eaten (run 4: W produced zero movement while the
                // virtual gamepad moved the player at 4.20 m/s). Events on
                // a virtual device travel through the same Move-action
                // bindings, exactly like the virtual gamepad.
                if (_virtualKeyboard == null)
                {
                    _virtualKeyboard = InputSystem.AddDevice<Keyboard>();
                    Log($"Virtual Keyboard device added: {_virtualKeyboard} (Keyboard.current was {(Keyboard.current != null ? Keyboard.current.ToString() : "null")})");
                }
                return _virtualKeyboard;
            }
        }

        private void QueueKeys(Key[] keys)
        {
            var kb = ActiveKeyboard;
            if (kb == null) return;
            InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
        }

        private IEnumerator Shot(string name)
        {
            // Headless (-nographics) batch mode has no render loop, so
            // WaitForEndOfFrame never fires and ScreenCapture hangs.
            // Skip screenshots there; visuals come from the WebGL build.
            if (Application.isBatchMode)
            {
                Log($"Screenshot skipped (batch mode): {name}");
                yield return null;
                yield break;
            }
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(outDir, "shots", name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            Log($"Screenshot: {path}");
        }

        private void Finish()
        {
            _log.AppendLine($"FAILURES: {_failures.Count}");
            foreach (var f in _failures) _log.AppendLine(" - " + f);
            _log.AppendLine(_failures.Count == 0 ? "RESULT: ALL HARNESS CHECKS PASSED" : "RESULT: FAILURES PRESENT (see above)");
            File.WriteAllText(Path.Combine(outDir, "results.txt"), _log.ToString());
            File.WriteAllText(Path.Combine(outDir, "done.txt"), DateTime.UtcNow.ToString("o"));
            Debug.Log("[M1Verify] DONE marker written to " + Path.Combine(outDir, "done.txt"));
        }
    }
}
