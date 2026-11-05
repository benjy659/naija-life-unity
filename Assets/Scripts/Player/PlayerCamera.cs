// Naija Streets — M1.2 Third-Person Camera
//
// OWNER HARD RULE (docs/DESIGN-INVENTORY.md §2, docs/MIGRATION-PLAN.md §6 M1):
//   One steady damped follow angle locked behind the player, walking and
//   driving. No sudden swings, no orbit drift, no automatic angle changes.
//   Occlusion pull-in so a building/wall can NEVER hide the player.
//   Camera-relative movement. Bias-corrected follow direction + dead zone so
//   strafing/backpedalling never swings the lens. Interiors use the player's
//   floor as camera floor (no world-Y clamp anywhere in this file).
//
// HOW IT WORKS
//   - This script owns ONE piece of state: followYaw (the camera's compass
//     heading) plus a fixed pitch. Nothing else ever rotates the camera.
//   - followYaw changes ONLY in two ways:
//       1. Manual orbit input, fed in by whoever owns input (see
//          AddOrbitInput). The player turning the camera is a user action,
//          not an automatic change.
//       2. Damped re-alignment toward the player's facing, and ONLY while the
//          player is actually moving forward: grounded, horizontal speed
//          above MinMoveSpeed, AND the angle between the velocity direction
//          and the facing direction is inside StrafeDeadZoneDegrees.
//          Strafing or backpedalling (velocity far off facing) suspends
//          re-alignment entirely, so the lens never swings. Idle never
//          re-aligns. The damping is exponential and framerate-independent,
//          so a turn around a corner is a smooth swing, never a snap.
//   - The yaw lives on a "CameraPivot" transform that this script positions
//     at the player's shoulder height every frame (relative to the PLAYER's
//     position, never a world-Y floor). A CinemachineCamera with
//     CinemachineThirdPersonFollow (Cinemachine 3.1.4) tracks that pivot:
//     CM supplies the position damping and the occlusion pull-in.
//   - Occlusion: ThirdPersonFollow.AvoidObstacles is enabled with fast
//     pull-in (DampingIntoCollision small) and slow return
//     (DampingFromCollision large). A wall between camera and player pulls
//     the camera in FRONT of the wall, so the player stays visible. No
//     occluder-fade is implemented (see KNOWN GAPS in docs/specs/camera-m1.2.md)
//     — the pull-in alone satisfies "never hide the player".
//
// WIRING (greybox scene, owned by the M1.1 controller agent)
//   - Drop this component on any GameObject in the scene (e.g. an empty
//     "PlayerCamera" object). It builds its own rig at runtime: a
//     CameraPivot, a CinemachineCamera, and a CinemachineBrain on the main
//     camera if one is missing. No prefab and no scene edit from this agent.
//   - Set FollowTarget to the player root Transform (the same root the
//     controller moves), or leave it empty: the camera auto-finds the
//     GameObject tagged "Player" and keeps retrying until one appears.
//   - The player (and its colliders) should be tagged "Player": CM ignores
//     obstacles with that tag, so the player's own body never pulls the
//     camera in.
//   - Camera-relative movement (M1.1): read GroundForward / GroundRight /
//     FollowYawDegrees from this component. They are the camera's ground
//     axes, guaranteed consistent with where the lens points.
//
// STATUS: UNVERIFIED — no Unity Editor exists on this VM yet (install in
// progress, separate agent). This file has never run. Do NOT call M1 done
// on code review alone. Verification checklist: docs/specs/camera-m1.2.md

using Unity.Cinemachine;
using UnityEngine;

namespace NaijaLife.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerCamera : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Target — the player controller's root Transform, plus its movement
        // state (horizontal speed, grounded). See IPlayerCameraTarget.
        // ------------------------------------------------------------------

        [Header("Target")]
        [Tooltip("Player root Transform (the same root the controller moves). " +
                 "Leave empty to auto-find the GameObject tagged \"Player\".")]
        public Transform followTarget;

        [Tooltip("Height of the camera pivot above the player's root position (m). " +
                 "Shoulder height. Relative to the PLAYER, never a world-Y floor.")]
        public float pivotHeight = 1.55f;

        // ------------------------------------------------------------------
        // Follow framing
        // ------------------------------------------------------------------

        [Header("Follow framing")]
        [Tooltip("Camera distance behind the player while on foot (m).")]
        public float walkDistance = 4.5f;

        [Tooltip("Camera distance behind the player while driving (m). " +
                 "M3 calls SetDrivingMode(true) to switch.")]
        public float drivingDistance = 6.5f;

        [Tooltip("Fixed downward pitch of the lens (deg). Positive = looking down at the player.")]
        [Range(-5f, 35f)]
        public float pitchDegrees = 12f;

        [Tooltip("How fast the camera distance eases between walk/drive (1/s).")]
        public float distanceDampRate = 2.5f;

        [Tooltip("Vertical field of view in landscape (desktop / landscape phone).")]
        [Range(40f, 80f)]
        public float landscapeFieldOfView = 60f;

        [Tooltip("Vertical field of view in portrait (phone held upright). " +
                 "Wider so the player doesn't fill a narrow screen.")]
        [Range(40f, 80f)]
        public float portraitFieldOfView = 68f;

        // ------------------------------------------------------------------
        // Follow-yaw rule — the bias correction + dead zone (owner hard rule)
        // ------------------------------------------------------------------

        [Header("Follow yaw — bias correction + dead zone")]
        [Tooltip("Below this horizontal speed the camera never re-aligns (m/s). " +
                 "Turning in place or standing still never swings the lens.")]
        public float minMoveSpeed = 0.6f;

        [Tooltip("Dead zone (deg) between velocity direction and facing direction. " +
                 "Inside it the player is moving roughly forward and the camera " +
                 "may re-align behind them. Outside it (strafe / backpedal) the " +
                 "camera heading is HELD — no swing, ever.")]
        [Range(5f, 90f)]
        public float strafeDeadZoneDegrees = 35f;

        [Tooltip("How fast the camera heading eases toward the player's facing " +
                 "while moving forward (1/s). Small = steady, no sudden swings.")]
        public float recenterRate = 1.6f;

        [Tooltip("After manual orbit input ends, re-alignment stays suspended " +
                 "for this long (s) so the camera doesn't fight the player.")]
        public float orbitRecenterDelay = 2f;

        [Tooltip("Manual orbit sensitivity (deg per unit of orbit input). " +
                 "Orbit input arrives via AddOrbitInput (mouse drag / right stick).")]
        public float orbitSensitivity = 120f;

        [Tooltip("Manual orbit pitch clamp (deg). Positive = looking down.")]
        public Vector2 orbitPitchClamp = new Vector2(-5f, 35f);

        // ------------------------------------------------------------------
        // Cinemachine rig tuning
        // ------------------------------------------------------------------

        [Header("Cinemachine rig")]
        [Tooltip("Position damping of the ThirdPersonFollow rig (camera-local axes, seconds). " +
                 "Small = rigid, large = squishy. Y is larger: vertical bob is damped hardest.")]
        public Vector3 rigDamping = new Vector3(0.25f, 0.45f, 0.25f);

        [Tooltip("Shoulder offset of the CM rig in pivot-local space. Keep centred: " +
                 "the owner rule wants ONE steady angle, not an over-shoulder offset.")]
        public Vector3 shoulderOffset = Vector3.zero;

        [Tooltip("Vertical arm length of the CM rig (m).")]
        public float verticalArmLength = 0.27f;

        [Tooltip("Layers the camera treats as occluders. Defaults to Everything; " +
                 "the player's own colliders are excluded by IgnoreTag = \"Player\".")]
        public LayerMask obstacleLayers = ~0;

        [Tooltip("Camera collision radius (m) — how close the lens may get to a wall.")]
        [Range(0.05f, 1f)]
        public float cameraCollisionRadius = 0.3f;

        [Tooltip("Pull-IN damping (s). SMALL = camera pulls in front of a wall FAST, " +
                 "so the player is hidden for as little as a frame as possible.")]
        [Range(0f, 2f)]
        public float dampingIntoCollision = 0.15f;

        [Tooltip("Pull-OUT damping (s). LARGE = camera eases back out slowly after " +
                 "an occlusion, no snapping, no oscillation.")]
        [Range(0f, 5f)]
        public float dampingFromCollision = 1.2f;

        [Tooltip("Virtual camera priority. Higher than any cutscene/flyover cam " +
                 "that should NOT take over during normal play.")]
        public int cameraPriority = 10;

        // ------------------------------------------------------------------
        // Public read state — for the player controller (camera-relative
        // movement) and for M3 driving.
        // ------------------------------------------------------------------

        /// <summary>Current camera heading in degrees (0 = +Z, clockwise from above).</summary>
        public float FollowYawDegrees => _followYaw;

        /// <summary>Camera forward projected onto the ground plane (unit length).</summary>
        public Vector3 GroundForward =>
            Quaternion.Euler(0f, _followYaw, 0f) * Vector3.forward;

        /// <summary>Camera right projected onto the ground plane (unit length).</summary>
        public Vector3 GroundRight =>
            Quaternion.Euler(0f, _followYaw, 0f) * Vector3.right;

        /// <summary>True once a follow target has been found and the rig snapped behind it.</summary>
        public bool IsReady { get; private set; }

        /// <summary>True while the CM rig is currently pulled in by an occluder.</summary>
        public bool IsOccluded =>
            _thirdPersonFollow != null && _thirdPersonFollow.CurrentObstacle != null;

        // ------------------------------------------------------------------
        // Internal state
        // ------------------------------------------------------------------

        private Transform _pivot;                  // rig origin; rotated by followYaw
        private CinemachineCamera _vcam;           // the virtual camera
        private CinemachineThirdPersonFollow _thirdPersonFollow;
        private float _followYaw;                  // THE camera heading (deg)
        private float _pitch;                      // current pitch (deg), orbit-adjustable
        private float _currentDistance;            // damped walk <-> drive distance
        private float _lastOrbitInputTime = -100f; // for orbitRecenterDelay
        private float _lastPortraitFov = -1f;      // aspect-bucket change detection
        private Vector3 _prevTargetPosition;       // fallback velocity source
        private Vector3 _smoothedFallbackVelocity;
        private bool _hasPrevTargetPosition;
        private bool _warnedNoTarget;
        private IPlayerCameraTarget _cameraTarget; // cached on target change (no per-frame GetComponent)
        private CharacterController _characterController;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            _pitch = pitchDegrees;
            _currentDistance = walkDistance;
            _followYaw = 0f;
            BuildRig();
        }

        private void Start()
        {
            ResolveTarget();
            CacheTargetComponents();
            if (followTarget != null)
                SnapToTarget();
        }

        private void Update()
        {
            if (followTarget == null)
            {
                ResolveTarget();
                if (followTarget == null)
                {
                    if (!_warnedNoTarget)
                    {
                        _warnedNoTarget = true;
                        Debug.LogWarning("[PlayerCamera] No follow target: assign FollowTarget " +
                                         "or tag the player GameObject \"Player\". Camera idle.");
                    }
                    return;
                }
                CacheTargetComponents();
                SnapToTarget();
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // 1. Read movement state (controller interface first, fallbacks after).
            ReadMovementState(out Vector3 velocity, out float horizontalSpeed,
                out bool grounded, out Vector3 facing);

            // 2. Update the ONE piece of camera state: followYaw.
            UpdateFollowYaw(velocity, horizontalSpeed, grounded, facing, dt);

            // 3. Place + rotate the pivot. CM reads it in LateUpdate (Brain),
            //    so setting it here guarantees the Brain sees the fresh pose.
            _pivot.position = followTarget.position + Vector3.up * pivotHeight;
            _pivot.rotation = Quaternion.Euler(_pitch, _followYaw, 0f);

            // 4. Ease the follow distance (walk <-> drive) and push it to CM.
            float targetDistance = _driving ? drivingDistance : walkDistance;
            _currentDistance = Damp(_currentDistance, targetDistance, distanceDampRate, dt);
            if (_thirdPersonFollow != null)
                _thirdPersonFollow.CameraDistance = _currentDistance;

            // 5. Portrait vs landscape FOV (phone-width acceptance).
            ApplyAspectFieldOfView();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Feed manual orbit input (mouse drag delta / right-stick deflection).
        /// Call every frame the input is active; call with Vector2.zero (or not
        /// at all) when idle. This is the ONLY way the camera heading changes
        /// other than the damped forward re-alignment — nothing orbits by itself.
        /// </summary>
        public void AddOrbitInput(Vector2 delta)
        {
            if (delta.sqrMagnitude <= 0f || followTarget == null)
                return;

            _followYaw += delta.x * orbitSensitivity * Time.deltaTime;
            _pitch = Mathf.Clamp(
                _pitch + delta.y * orbitSensitivity * Time.deltaTime,
                orbitPitchClamp.x, orbitPitchClamp.y);
            _lastOrbitInputTime = Time.time;
        }

        /// <summary>
        /// Point the camera at a new target (player root, or a vehicle in M3).
        /// Snaps the lens behind the new target immediately — no swoop.
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            _hasPrevTargetPosition = false;
            _warnedNoTarget = false;
            CacheTargetComponents();
            if (target != null)
                SnapToTarget();
        }

        /// <summary>
        /// M3 driving: true while the player is driving a vehicle. Eases the
        /// camera out to drivingDistance. Call with false on exit.
        /// </summary>
        public void SetDrivingMode(bool driving)
        {
            _driving = driving;
        }

        /// <summary>
        /// Snap the lens behind the player RIGHT NOW: heading = facing, pivot
        /// placed, vcam transform parked at the desired pose so the Brain does
        /// not blend/swoop on spawn, teleport, or target change. Use after
        /// spawn, fast travel (journey-home is travel, not teleport — but the
        /// same snap applies on any warp), and scene load.
        /// </summary>
        public void SnapToTarget()
        {
            if (followTarget == null)
                return;

            ReadMovementState(out _, out _, out _, out Vector3 facing);
            float facingYaw = FacingYaw(facing);
            _followYaw = facingYaw;
            _pitch = pitchDegrees;

            _pivot.position = followTarget.position + Vector3.up * pivotHeight;
            _pivot.rotation = Quaternion.Euler(_pitch, _followYaw, 0f);

            if (_vcam != null)
            {
                // Park the vcam at the pose CM will compute, so the first live
                // frame is already correct (no blend swoop from the scene-view pose).
                Vector3 hand = _pivot.position
                    + _pivot.rotation * shoulderOffset
                    + _pivot.up * -verticalArmLength;
                _vcam.transform.position = hand - _pivot.forward * _currentDistance;
                _vcam.transform.rotation = _pivot.rotation;
            }

            IsReady = true;
        }

        // ------------------------------------------------------------------
        // Follow-yaw rule (the owner hard rule, in code)
        // ------------------------------------------------------------------

        private bool _driving;

        private void UpdateFollowYaw(Vector3 velocity, float horizontalSpeed,
            bool grounded, Vector3 facing, float dt)
        {
            // Manual orbit already applied followYaw in AddOrbitInput; after it
            // ends, hold re-alignment for orbitRecenterDelay so the camera
            // doesn't fight the player.
            if (Time.time - _lastOrbitInputTime < orbitRecenterDelay)
                return;

            // No re-alignment unless the player is actually travelling forward.
            if (!grounded || horizontalSpeed < minMoveSpeed)
                return;

            Vector3 flatVelocity = velocity;
            flatVelocity.y = 0f;
            if (flatVelocity.sqrMagnitude < 0.0001f)
                return;

            float velocityYaw = Mathf.Atan2(flatVelocity.x, flatVelocity.z) * Mathf.Rad2Deg;
            float facingYaw = FacingYaw(facing);

            // BIAS CORRECTION + DEAD ZONE: compare the direction of travel with
            // the CAMERA heading, not with the character's facing. The M1.1
            // controller always turns the body toward its travel direction, so
            // velocity and facing agree within a frame or two of any input.
            // Testing against facing (the first version) let a held strafe or
            // backpedal re-align the camera, which turned the camera-relative
            // input with it: holding D circled the player forever (Claude
            // review 2026-11-05, docs/reviews/m1-review.md, finding R1).
            // Only travel roughly along the lens re-aligns it.
            float moveVsCamera = Mathf.Abs(Mathf.DeltaAngle(velocityYaw, _followYaw));
            if (moveVsCamera > strafeDeadZoneDegrees)
                return;

            // Damped re-alignment: exponential, framerate-independent.
            // A turn around a corner is a smooth swing behind the player,
            // never a snap, and never an orbit — the heading only ever chases
            // the player's facing, and only while moving forward.
            _followYaw = DampAngle(_followYaw, facingYaw, recenterRate, dt);
        }

        // ------------------------------------------------------------------
        // Movement state — controller interface first, then fallbacks
        // ------------------------------------------------------------------

        private void ReadMovementState(out Vector3 velocity, out float horizontalSpeed,
            out bool grounded, out Vector3 facing)
        {
            velocity = Vector3.zero;
            horizontalSpeed = 0f;
            grounded = true;
            facing = Vector3.zero;

            if (followTarget == null)
                return;

            // 1. Authoritative: the controller implements IPlayerCameraTarget.
            var info = _cameraTarget;
            if (info != null)
            {
                velocity = info.Velocity;
                horizontalSpeed = info.HorizontalSpeed;
                grounded = info.IsGrounded;
                facing = info.Facing;
                if (facing.sqrMagnitude < 0.0001f)
                    facing = followTarget.forward; // no facing supplied — use the root
                return;
            }

            // 2. CharacterController velocity (the M1 plan uses CharacterController).
            var characterController = _characterController;
            if (characterController != null)
            {
                velocity = characterController.velocity;
                grounded = characterController.isGrounded;
            }
            else
            {
                // 3. Last resort: velocity from transform deltas, smoothed so
                //    frame jitter can't trip the dead-zone test.
                Vector3 now = followTarget.position;
                if (_hasPrevTargetPosition && Time.deltaTime > 0f)
                {
                    Vector3 raw = (now - _prevTargetPosition) / Time.deltaTime;
                    _smoothedFallbackVelocity = Vector3.Lerp(
                        _smoothedFallbackVelocity, raw, 1f - Mathf.Exp(-10f * Time.deltaTime));
                    velocity = _smoothedFallbackVelocity;
                }
                _prevTargetPosition = now;
                _hasPrevTargetPosition = true;
            }

            Vector3 flat = velocity;
            flat.y = 0f;
            horizontalSpeed = flat.magnitude;
            facing = followTarget.forward;
        }

        private static float FacingYaw(Vector3 facing)
        {
            Vector3 flat = facing;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f)
                return 0f;
            return Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------
        // Rig construction — self-contained, no prefab, no scene edit needed
        // ------------------------------------------------------------------

        private void BuildRig()
        {
            // Pivot: the rig origin. Positioned + rotated by this script;
            // CinemachineThirdPersonFollow reads its rotation for the shot.
            var pivotGo = new GameObject("CameraPivot");
            pivotGo.transform.SetParent(transform, false);
            _pivot = pivotGo.transform;

            // Main camera + Brain (the Brain is what makes a vcam drive the camera).
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = FindFirstObjectByType<Camera>();
                if (mainCam == null)
                {
                    Debug.LogError("[PlayerCamera] No Camera in the scene — add a Main Camera. " +
                                   "Rig built, but nothing will render through it.");
                }
            }
            if (mainCam != null)
            {
                var brain = mainCam.GetComponent<CinemachineBrain>();
                if (brain == null)
                    brain = mainCam.gameObject.AddComponent<CinemachineBrain>();
                brain.enabled = true;
            }

            // Virtual camera + ThirdPersonFollow body.
            var vcamGo = new GameObject("PlayerCameraRig");
            vcamGo.transform.SetParent(transform, false);
            _vcam = vcamGo.AddComponent<CinemachineCamera>();
            _vcam.Follow = _pivot;
            _vcam.Priority = cameraPriority;

            _thirdPersonFollow = _vcam.AddCinemachineComponent<CinemachineThirdPersonFollow>();
            _thirdPersonFollow.ShoulderOffset = shoulderOffset;
            _thirdPersonFollow.VerticalArmLength = verticalArmLength;
            _thirdPersonFollow.CameraSide = 0.5f; // centred — one steady angle, no over-shoulder offset
            _thirdPersonFollow.CameraDistance = walkDistance;
            _thirdPersonFollow.Damping = rigDamping;
            _thirdPersonFollow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings
            {
                Enabled = true,
                CollisionFilter = obstacleLayers,
                CameraRadius = cameraCollisionRadius,
                DampingIntoCollision = dampingIntoCollision,
                DampingFromCollision = dampingFromCollision,
                IgnoreTag = "Player"
            };

            ApplyLens();
        }

        private void ApplyLens()
        {
            if (_vcam == null)
                return;

            float fov = Screen.width < Screen.height ? portraitFieldOfView : landscapeFieldOfView;
            var lens = _vcam.Lens;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.05f;
            lens.FarClipPlane = 1000f;
            _vcam.Lens = lens;
        }

        private void ApplyAspectFieldOfView()
        {
            // Only touch the lens when the portrait/landscape bucket changes.
            float bucket = Screen.width < Screen.height ? 1f : 0f;
            if (!Mathf.Approximately(bucket, _lastPortraitFov))
            {
                _lastPortraitFov = bucket;
                ApplyLens();
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private void ResolveTarget()
        {
            if (followTarget != null)
                return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                followTarget = player.transform;
        }

        /// <summary>
        /// Cache the movement-state sources for the current target so Update
        /// does no per-frame GetComponent calls. Re-run on every target change.
        /// </summary>
        private void CacheTargetComponents()
        {
            _cameraTarget = followTarget != null
                ? followTarget.GetComponent<IPlayerCameraTarget>()
                : null;
            _characterController = followTarget != null
                ? followTarget.GetComponent<CharacterController>()
                : null;
        }

        private static float Damp(float current, float target, float rate, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
        }

        private static float DampAngle(float current, float target, float rate, float dt)
        {
            float delta = Mathf.DeltaAngle(current, target);
            return current + delta * (1f - Mathf.Exp(-rate * dt));
        }
    }
}
