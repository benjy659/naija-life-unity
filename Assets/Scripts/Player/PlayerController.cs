// Naija Streets (Unity rebuild) — M1.1 Player Controller
// Spec: docs/DESIGN-INVENTORY.md §2 (Player) and docs/MIGRATION-PLAN.md §6 M1.
//   - Third-person, CharacterController-based movement.
//   - 1D speed-blend locomotion: idle / walk / jog / run / sprint.
//     Walk < 2.6 m/s, default jog 4.2 m/s (design inventory §2).
//   - Camera-relative movement; grounded check; slope handling.
//   - Foot-slide kept minimal: speed is smoothed (accel/decel ramps, no
//     velocity snaps) and the Animator blend parameter is driven from the
//     real horizontal speed, so stride grows with speed once clips exist.
//   - NO camera code here — the M1.2 camera agent owns the camera. This
//     controller only READS Camera.main yaw for camera-relative movement and
//     exposes the public interface below for the camera to follow.
//   - NO animation clips exist yet in the project (Nov 5, 2026). The
//     Animator Controller (Assets/Art/Animations/PlayerLocomotion.controller)
//     has the "Speed" float parameter and an empty 1D blend tree; the blend
//     parameter is driven every frame, but with no clips there is NO visible
//     walk animation yet — the player is a capsule placeholder. Do NOT treat
//     the blend parameter as proof of animation; clips are a later task.
//
// Public interface for the camera builder (M1.2) — do not rename without
// coordinating with the camera agent:
//   PlayerRoot    — the player root Transform (this transform). The M1.2
//                   camera follows THIS (it adds its own 1.55 m pivot
//                   height); it auto-finds the root by the "Player" tag.
//   FollowTarget  — head-height child Transform (1.6 m). A head-height
//                   anchor for other consumers (name tags, aim, etc.) —
//                   NOT the camera's follow target, or the camera would
//                   double-count height.
//   CurrentSpeed  — current horizontal speed in m/s.
//   IsGrounded    — true while the CharacterController is grounded.
//   Velocity      — current full velocity vector (horizontal + vertical).
// This controller also implements IPlayerCameraTarget (Velocity /
// HorizontalSpeed / IsGrounded / Facing) so the M1.2 camera reads
// authoritative movement state instead of inferring it from transforms.
//
// UNVERIFIED (Nov 5, 2026): no Unity Editor exists on this VM yet, so this
// code has never run. See Assets/Scripts/Player/README_M1.1.md for the
// verification checklist. Never claim it works on code review alone.

using UnityEngine;

namespace NaijaLife.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, IPlayerCameraTarget
    {
        [Header("Speeds (m/s) — walk < 2.6, default jog 4.2 per DESIGN-INVENTORY §2")]
        [Tooltip("Walk speed. Must stay under 2.6 m/s per the design inventory.")]
        [SerializeField] private float walkSpeed = 2.4f;
        [Tooltip("Default move speed (WASD at full input, joystick at full tilt).")]
        [SerializeField] private float jogSpeed = 4.2f;
        [Tooltip("Fast run, reached at high joystick tilt / run modifier. Blend band between jog and sprint.")]
        [SerializeField] private float runSpeed = 6.5f;
        [Tooltip("Sprint speed while Sprint is held.")]
        [SerializeField] private float sprintSpeed = 8.5f;

        [Header("Feel")]
        [Tooltip("How fast horizontal speed ramps up (m/s^2). Keeps feet planted — no velocity snaps.")]
        [SerializeField] private float acceleration = 12f;
        [Tooltip("How fast horizontal speed ramps down when input stops.")]
        [SerializeField] private float deceleration = 16f;
        [Tooltip("Turn rate toward the movement direction, degrees/second.")]
        [SerializeField] private float turnSpeed = 720f;
        [Tooltip("Gravity applied while airborne.")]
        [SerializeField] private float gravity = -22f;
        [Tooltip("Small downward force while grounded so the controller sticks to the ground on small drops.")]
        [SerializeField] private float groundedStickForce = -3f;
        [Tooltip("Input magnitude below this is treated as no input (stick dead zone).")]
        [SerializeField] private float inputDeadZone = 0.1f;

        [Header("Slope")]
        [Tooltip("Max climbable slope in degrees (matches CharacterController.slopeLimit).")]
        [SerializeField] private float slopeLimit = 45f;
        [Tooltip("How fast the player slides down slopes steeper than slopeLimit.")]
        [SerializeField] private float slopeSlideSpeed = 4f;
        [Tooltip("How far below the feet to probe for the ground normal (slope detection).")]
        [SerializeField] private float groundProbeDistance = 1.2f;

        [Header("References")]
        [Tooltip("Input wrapper (same GameObject). Auto-found if left empty.")]
        [SerializeField] private PlayerInput playerInput;
        [Tooltip("Animator on this GameObject (optional). If present, 'Speed' is driven from real speed.")]
        [SerializeField] private Animator animator;
        [Tooltip("Camera follow target — child Transform at head height. Auto-found by name if left empty.")]
        [SerializeField] private Transform followTarget;
        [Tooltip("Animator float parameter driven from horizontal speed.")]
        [SerializeField] private string speedParameter = "Speed";

        // ---- Public interface for the camera builder (M1.2) ----
        /// <summary>Player root Transform (this transform).</summary>
        public Transform PlayerRoot => transform;

        /// <summary>Transform the camera follows (head-height child).</summary>
        public Transform FollowTarget => followTarget;

        /// <summary>Current horizontal speed in m/s.</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>True while the CharacterController is grounded.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>Current full velocity (horizontal + vertical).</summary>
        public Vector3 Velocity { get; private set; }

        // ---- IPlayerCameraTarget (M1.2 camera reads authoritative state) ----
        /// <summary>Horizontal speed, m/s — same value as CurrentSpeed.</summary>
        public float HorizontalSpeed => CurrentSpeed;

        /// <summary>Direction the character model faces (horizontal plane).</summary>
        public Vector3 Facing
        {
            get
            {
                Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                return facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector3.zero;
            }
        }

        /// <summary>Current locomotion band, derived from CurrentSpeed.</summary>
        public LocomotionBand CurrentBand
        {
            get
            {
                if (CurrentSpeed < 0.1f) return LocomotionBand.Idle;
                if (CurrentSpeed < walkSpeed + 0.05f) return LocomotionBand.Walk;
                if (CurrentSpeed < jogSpeed + 0.05f) return LocomotionBand.Jog;
                if (CurrentSpeed < sprintSpeed - 0.5f) return LocomotionBand.Run;
                return LocomotionBand.Sprint;
            }
        }

        public enum LocomotionBand { Idle, Walk, Jog, Run, Sprint }

        private CharacterController _controller;
        private Vector3 _horizontalVelocity; // smoothed horizontal velocity
        private float _verticalVelocity;
        private Vector3 _groundNormal = Vector3.up;
        private int _speedParameterHash;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();
            if (animator == null)
                animator = GetComponent<Animator>();
            if (followTarget == null)
            {
                Transform found = transform.Find("FollowTarget");
                if (found != null)
                    followTarget = found;
            }

            // Keep the CharacterController's own slope/step settings in sync
            // with the serialized fields so the prefab values are the truth.
            _controller.slopeLimit = slopeLimit;
            _controller.stepOffset = 0.3f;
            _controller.skinWidth = 0.08f;

            _speedParameterHash = Animator.StringToHash(speedParameter);
        }

        private void Update()
        {
            if (playerInput == null)
            {
                // No input source: stand still but keep gravity + animator updated.
                ApplyMotion(Vector3.zero, 0f);
                return;
            }

            Vector2 input = playerInput.MoveInput;

            // Camera-relative movement. The camera agent owns the camera; we
            // only read its yaw. If no camera exists yet (greybox without the
            // camera rig), fall back to the player's own facing.
            float cameraYaw = GetCameraYaw();
            Quaternion yawRotation = Quaternion.Euler(0f, cameraYaw, 0f);
            Vector3 forward = yawRotation * Vector3.forward;
            Vector3 right = yawRotation * Vector3.right;

            Vector3 wishDirection = forward * input.y + right * input.x;
            if (wishDirection.sqrMagnitude > 1f)
                wishDirection.Normalize();

            float inputMagnitude = Mathf.Clamp01(input.magnitude);
            if (inputMagnitude < inputDeadZone)
            {
                wishDirection = Vector3.zero;
                inputMagnitude = 0f;
            }

            // Speed band: sprint > walk > default jog. Joystick tilt scales
            // the band speed continuously (partial tilt = slower), so a light
            // touch walks and a full tilt jogs/runs.
            float bandSpeed = jogSpeed;
            if (playerInput.SprintHeld)
                bandSpeed = sprintSpeed;
            else if (playerInput.WalkHeld)
                bandSpeed = walkSpeed;
            else if (inputMagnitude > 0.85f)
                bandSpeed = runSpeed;

            float targetSpeed = bandSpeed * inputMagnitude;
            ApplyMotion(wishDirection, targetSpeed);
        }

        private void ApplyMotion(Vector3 wishDirection, float targetSpeed)
        {
            float dt = Time.deltaTime;

            // --- Horizontal speed: smooth ramp, never a snap (feet planted). ---
            float currentHorizontalSpeed = _horizontalVelocity.magnitude;
            float rate = targetSpeed > currentHorizontalSpeed ? acceleration : deceleration;
            float newSpeed = Mathf.MoveTowards(currentHorizontalSpeed, targetSpeed, rate * dt);

            Vector3 desiredHorizontal;
            if (wishDirection.sqrMagnitude > 0.0001f)
            {
                desiredHorizontal = wishDirection.normalized * newSpeed;
                // Face the movement direction (smooth turn, not a snap).
                Quaternion targetRotation = Quaternion.LookRotation(wishDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, turnSpeed * dt);
            }
            else
            {
                // No input: keep the last heading, bleed speed to zero.
                desiredHorizontal = _horizontalVelocity.normalized * newSpeed;
                if (newSpeed < 0.01f)
                    desiredHorizontal = Vector3.zero;
            }
            _horizontalVelocity = desiredHorizontal;

            // --- Grounding + slope probe (before the move, so the slide uses fresh data). ---
            UpdateGrounded();

            // --- Vertical: gravity + stick-to-ground. ---
            if (IsGrounded)
            {
                if (_verticalVelocity < 0f)
                    _verticalVelocity = groundedStickForce;
            }
            else
            {
                _verticalVelocity += gravity * dt;
                _verticalVelocity = Mathf.Max(_verticalVelocity, -30f);
            }

            Vector3 motion = _horizontalVelocity + Vector3.up * _verticalVelocity;

            // --- Slope slide: on ground steeper than slopeLimit, slide down it. ---
            // CharacterController refuses to climb > slopeLimit but does not
            // slide on its own, so a steep slope would otherwise be standable.
            if (IsGrounded && Vector3.Angle(_groundNormal, Vector3.up) > slopeLimit)
            {
                Vector3 slideDirection = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
                motion += slideDirection * slopeSlideSpeed;
            }

            _controller.Move(motion * dt);

            // --- Public state + animator. ---
            Vector3 horizontal = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z);
            CurrentSpeed = horizontal.magnitude;
            Velocity = _controller.velocity;

            if (animator != null)
                animator.SetFloat(_speedParameterHash, CurrentSpeed);
        }

        /// <summary>
        /// Grounded state: CharacterController.isGrounded, backed by a short
        /// downward probe that also reads the ground normal for slope logic.
        /// The probe uses the controller's own footprint so kerbs and small
        /// steps do not read as airborne.
        /// </summary>
        private void UpdateGrounded()
        {
            IsGrounded = _controller.isGrounded;

            Vector3 origin = transform.position + Vector3.up * (_controller.height * 0.5f);
            float probeLength = _controller.height * 0.5f + groundProbeDistance;
            if (Physics.SphereCast(origin, _controller.radius * 0.9f, Vector3.down,
                    out RaycastHit hit, probeLength, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                _groundNormal = hit.normal;
                // Close to the ground counts as grounded even if isGrounded
                // flickers for a frame (e.g. walking down a kerb).
                if (hit.distance <= _controller.height * 0.5f + 0.15f)
                    IsGrounded = true;
            }
            else
            {
                _groundNormal = Vector3.up;
            }
        }

        /// <summary>Yaw of the main camera. Falls back to the player's own yaw when no camera exists yet.</summary>
        private float GetCameraYaw()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                return mainCamera.transform.eulerAngles.y;
            return transform.eulerAngles.y;
        }
    }
}
