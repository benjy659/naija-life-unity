// M3 getting in and out (docs/specs/vehicles-m3.md sections 5.2, 5.3, 7).
// On the player root (next to PlayerController). Owns the walk to the door,
// the seat, the layer swap that keeps the lens clear of the player's own car,
// the camera calls, and the exit-point checks. Never teleports across the map,
// never places the player inside a wall or a car.
//
// Prompt: this script only exposes the offer (CurrentOffer, OfferChanged).
// VehiclePromptBridge hands it to the HUD's one prompt (NLPrompts) when the HUD
// is in the scene; without the HUD, showDebugPrompt draws a plain debug label.
//
// UNVERIFIED: no Unity Editor in Claude's container (2026-11-06).

using System;
using System.Collections;
using System.Collections.Generic;
using NaijaLife.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NaijaLife.Vehicles
{
    public enum DriveState { OnFoot, WalkingToDoor, Driving }

    [DisallowMultipleComponent]
    public sealed class DriveSession : MonoBehaviour
    {
        [Tooltip("Hide the player's renderers while seated. M3 only: the greybox has no sit pose, " +
                 "and a standing body clips through a saloon roof. M4 replaces this with a sit pose.")]
        public bool hidePlayerWhileSeated = true;

        [Tooltip("Walk speed to the door (m/s), the M1 walk band.")]
        public float walkToDoorSpeed = 2.4f;

        [Tooltip("Give up the walk to the door after this long (s) and keep the offer.")]
        public float walkToDoorTimeout = 1.5f;

        public bool showDebugPrompt = true;

        public DriveState State { get; private set; } = DriveState.OnFoot;
        public VehicleController CurrentVehicle { get; private set; }

        /// <summary>The parked drivable the player could get into now, or null.</summary>
        public VehicleController CurrentOffer { get; private set; }

        /// <summary>True while driving and the exit is blocked (all exit points occupied) or too fast.</summary>
        public bool ExitBlocked { get; private set; }

        public event Action<VehicleController> OfferChanged;
        public event Action<VehicleController> Entered;
        public event Action<VehicleController> Exited;

        CharacterController _cc;
        PlayerController _controller;
        VehiclePlayerDriver _driver;
        PlayerCamera _camera;
        Transform _cameraTargetBefore;
        Transform _parentBefore;
        Animator _animator;
        int _speedHash;
        bool _animatorHasSpeed;
        Renderer[] _renderers;
        readonly Dictionary<GameObject, int> _savedLayers = new Dictionary<GameObject, int>();
        InputAction _interact;
        bool _interactRequested;
        float _blockedFlashUntil;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _controller = GetComponent<PlayerController>();
            _driver = GetComponent<VehiclePlayerDriver>();
            if (_driver == null) _driver = gameObject.AddComponent<VehiclePlayerDriver>();
            _driver.enabled = false;
            _animator = GetComponentInChildren<Animator>();
            _speedHash = Animator.StringToHash("Speed");
            if (_animator != null)
                foreach (var p in _animator.parameters)
                    if (p.nameHash == _speedHash && p.type == AnimatorControllerParameterType.Float) _animatorHasSpeed = true;

            _interact = new InputAction("Interact", InputActionType.Button);
            _interact.AddBinding("<Keyboard>/f");
            _interact.AddBinding("<Gamepad>/buttonNorth");
        }

        void OnEnable() { _interact.Enable(); }
        void OnDisable() { _interact.Disable(); }
        void OnDestroy() { _interact.Dispose(); }

        /// <summary>For the touch Drive/Exit button (M3.4) and tests.</summary>
        public void RequestInteract() { _interactRequested = true; }

        void Update()
        {
            bool pressed = _interact.WasPressedThisFrame() || _interactRequested;
            _interactRequested = false;

            switch (State)
            {
                case DriveState.OnFoot:
                    UpdateOffer();
                    if (pressed && CurrentOffer != null)
                        StartCoroutine(EnterRoutine(CurrentOffer));
                    break;

                case DriveState.Driving:
                    ExitBlocked = !VehicleMath.CanExit(CurrentVehicle.ForwardSpeed);
                    if (pressed) TryExit();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Offer (spec 7.1 steps 1-2)
        // ------------------------------------------------------------------

        void UpdateOffer()
        {
            VehicleController best = null;
            float bestD = float.MaxValue;
            Vector3 me = transform.position;
            var all = VehicleController.All;
            for (int i = 0; i < all.Count; i++)
            {
                var v = all[i];
                if (!v.playerDrivable || v.Driver != VehicleDriver.None || v.Seat == null || v.Seat.doorPoint == null) continue;
                Vector3 to = v.Seat.doorPoint.position - me;
                if (Mathf.Abs(to.y) > 1.2f) continue;
                to.y = 0f;
                float d = to.magnitude;
                // Hysteresis (HUD spec): offer inside 2.5 m, keep the current offer to 3.0 m.
                float limit = v == CurrentOffer ? VehicleSeat.WithdrawDistance : VehicleSeat.OfferDistance;
                if (d > limit || (best != null && d > bestD)) continue;
                // Roughly facing the vehicle, unless already at the door.
                Vector3 toBody = v.transform.position - me; toBody.y = 0f;
                if (d > 1.2f && Vector3.Dot(transform.forward, toBody.normalized) < -0.2f) continue;
                best = v; bestD = d;
            }
            if (best != CurrentOffer)
            {
                CurrentOffer = best;
                OfferChanged?.Invoke(best);
            }
        }

        // ------------------------------------------------------------------
        // Enter (spec 7.1 steps 3-5, camera 5.2, layers 5.3)
        // ------------------------------------------------------------------

        IEnumerator EnterRoutine(VehicleController v)
        {
            State = DriveState.WalkingToDoor;
            SetOffer(null);
            if (_controller != null) _controller.enabled = false;

            Vector3 door = v.Seat.doorPoint.position;
            float t = 0f, vy = 0f;
            while (true)
            {
                Vector3 to = door - transform.position; to.y = 0f;
                if (to.magnitude < 0.15f) break;
                if (t > walkToDoorTimeout || v.Driver != VehicleDriver.None)
                {
                    // Blocked or someone else took it: cancel, keep walking normal.
                    SetAnimSpeed(0f);
                    if (_controller != null) _controller.enabled = true;
                    State = DriveState.OnFoot;
                    yield break;
                }
                Vector3 step = to.normalized * Mathf.Min(walkToDoorSpeed * Time.deltaTime, to.magnitude);
                vy = _cc.isGrounded ? -1f : vy - 9.81f * Time.deltaTime;
                _cc.Move(step + Vector3.up * vy * Time.deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(to.normalized, Vector3.up), 540f * Time.deltaTime);
                SetAnimSpeed(walkToDoorSpeed);
                t += Time.deltaTime;
                yield return null;
            }
            SetAnimSpeed(0f);
            Seat(v);
        }

        void Seat(VehicleController v)
        {
            CurrentVehicle = v;
            v.SetDriver(VehicleDriver.Player);

            _cc.enabled = false;
            _parentBefore = transform.parent;
            transform.SetParent(v.Seat.seatPoint, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (hidePlayerWhileSeated) SetRenderersVisible(false);

            // The lens sits inside this car's body: its colliders must not block it (spec 5.3).
            VehicleLayers.SetColliderLayers(v.transform, VehicleLayers.DrivenVehicle, _savedLayers);
            v.Body.isKinematic = false;
            v.Body.interpolation = RigidbodyInterpolation.Interpolate;
            v.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // no tunnelling at 140 km/h
            v.Body.WakeUp();

            _driver.Begin(v);

            _camera = FindCamera();
            if (_camera != null)
            {
                _cameraTargetBefore = _camera.followTarget;
                var def = v.definition;
                _camera.SetDrivingMode(true, def != null ? def.cameraDistance : 6.5f, def != null ? def.cameraPivotHeight : 1.9f);
                _camera.SetFollowTarget(v.transform, false); // never SnapToTarget on enter (spec 5.2)
            }

            State = DriveState.Driving;
            Entered?.Invoke(v);
        }

        // ------------------------------------------------------------------
        // Exit (spec 7.2)
        // ------------------------------------------------------------------

        /// <summary>Returns true if the player got out.</summary>
        public bool TryExit()
        {
            if (State != DriveState.Driving || CurrentVehicle == null) return false;
            var v = CurrentVehicle;
            if (!VehicleMath.CanExit(v.ForwardSpeed)) { _blockedFlashUntil = Time.time + 1.5f; return false; }

            var points = v.Seat.exitPoints ?? Array.Empty<Transform>();
            var blocked = new bool[points.Length];
            var placed = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
                blocked[i] = points[i] == null || !ExitPointFree(points[i].position, out placed[i]);
            int pick = VehicleMath.FirstFreeExit(blocked);
            if (pick < 0) { ExitBlocked = true; _blockedFlashUntil = Time.time + 1.5f; return false; }

            _driver.End(); // parks: handbrake on, pedals reset
            v.SetDriver(VehicleDriver.None);
            VehicleLayers.RestoreLayers(_savedLayers);
            v.Body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            transform.SetParent(_parentBefore, true);
            float yaw = v.transform.eulerAngles.y;
            transform.SetPositionAndRotation(placed[pick], Quaternion.Euler(0f, yaw, 0f));
            _cc.enabled = true; // after the position is set, or the CC snaps back
            if (hidePlayerWhileSeated) SetRenderersVisible(true);
            if (_controller != null) _controller.enabled = true;

            if (_camera != null)
            {
                _camera.SetDrivingMode(false);
                _camera.SetFollowTarget(_cameraTargetBefore != null ? _cameraTargetBefore : transform, false);
            }

            CurrentVehicle = null;
            State = DriveState.OnFoot;
            ExitBlocked = false;
            Exited?.Invoke(v);
            return true;
        }

        /// <summary>
        /// A standing capsule the size of the player's CharacterController fits at
        /// the point (on the ground found below it), touching nothing but the floor.
        /// </summary>
        bool ExitPointFree(Vector3 p, out Vector3 standAt)
        {
            standAt = p;
            float radius = _cc != null ? _cc.radius : 0.35f;
            float height = _cc != null ? _cc.height : 1.8f;
            int mask = VehicleLayers.ExitBlockMask;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit floor, 3.5f, mask, QueryTriggerInteraction.Ignore))
                standAt = floor.point;
            else
                return false; // no floor (off the bridge edge): never drop the player
            Vector3 bottom = standAt + Vector3.up * (radius + 0.08f);
            Vector3 top = standAt + Vector3.up * (height - radius);
            return !Physics.CheckCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        void SetOffer(VehicleController v)
        {
            if (CurrentOffer == v) return;
            CurrentOffer = v;
            OfferChanged?.Invoke(v);
        }

        void SetAnimSpeed(float s)
        {
            if (_animatorHasSpeed) _animator.SetFloat(_speedHash, s);
        }

        void SetRenderersVisible(bool visible)
        {
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in _renderers) if (r != null) r.enabled = visible;
        }

        PlayerCamera FindCamera()
        {
            if (_camera != null) return _camera;
            return FindFirstObjectByType<PlayerCamera>();
        }

        void OnGUI()
        {
            if (!showDebugPrompt) return;
            string label = null;
            if (State == DriveState.OnFoot && CurrentOffer != null)
                label = "[F] Drive " + (CurrentOffer.definition != null ? CurrentOffer.definition.displayName : "");
            else if (State == DriveState.Driving && Time.time < _blockedFlashUntil)
                label = ExitBlocked ? "Stop first / no room to get out" : "No room to get out";
            if (label == null) return;
            var r = new Rect(Screen.width * 0.5f - 140f, Screen.height - 120f, 280f, 32f);
            GUI.Box(r, label);
        }
    }
}
