// M3 vehicle controller (docs/specs/vehicles-m3.md section 6).
// Rigidbody + 4 WheelColliders, arcade tuned. The ONLY input is SetInput(DriveInput):
// the player (VehiclePlayerDriver), bots (TrafficDriver) and later the network
// all drive it the same way (spec section 9).
//
// Also the camera target while driving (IPlayerCameraTarget, spec section 5):
// Facing = nose, Velocity = body velocity, IsGrounded = 2+ wheels touching.
//
// UNVERIFIED: written by Claude 2026-11-06 without a Unity Editor. Compile-checked
// against UnityEngine 2021.3 reference assemblies only. Ted's team: first run in
// Greybox_M3 (Naija > M3 > Build Test Scene), then tune numbers in the assets.

using System.Collections.Generic;
using NaijaLife.Player;
using UnityEngine;

namespace NaijaLife.Vehicles
{
    public enum VehicleDriver { None, Player, Bot, Remote }

    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class VehicleController : MonoBehaviour, IPlayerCameraTarget
    {
        [System.Serializable]
        public struct Wheel
        {
            public WheelCollider collider;
            [Tooltip("Visual mesh, pivot at the hub. Spun and steered from the collider pose.")]
            public Transform visual;
        }

        public VehicleDefinition definition;

        [Header("Wheels (left = driver side in Nigeria)")]
        public Wheel frontLeft;
        public Wheel frontRight;
        public Wheel rearLeft;
        public Wheel rearRight;

        [Tooltip("Parked vehicles offer 'Drive' to the player. Bot traffic does not (taking it is a crime, later).")]
        public bool playerDrivable = true;

        // ---- Registry (TrafficSense and DriveSession read it; no FindObjectsOfType per frame) ----
        static readonly List<VehicleController> _all = new List<VehicleController>();
        public static IReadOnlyList<VehicleController> All => _all;
        static int _nextId = 1;

        public int Id { get; private set; }
        public VehicleDriver Driver { get; private set; } = VehicleDriver.None;
        public bool EngineOn { get; set; } = true;
        public bool IsKinematicMover => _rb != null && _rb.isKinematic;
        public Rigidbody Body => _rb;
        public VehicleSeat Seat { get; private set; }
        public PedalLogic Pedals => _pedals;
        public DriveInput LastInput => _input;

        /// <summary>Signed speed along the nose (m/s).</summary>
        public float ForwardSpeed => Vector3.Dot(Velocity, transform.forward);
        public float SpeedKmh => Mathf.Abs(ForwardSpeed) * VehicleMath.KmhPerMs;

        // IPlayerCameraTarget
        public Vector3 Velocity => _rb == null ? Vector3.zero : (_rb.isKinematic ? _kinematicVelocity : VehicleRb.GetVelocity(_rb));
        public float HorizontalSpeed { get { var v = Velocity; v.y = 0f; return v.magnitude; } }
        public bool IsGrounded => _rb != null && (_rb.isKinematic || _groundedWheels >= 2);
        public Vector3 Facing => transform.forward;

        /// <summary>Half extents of the body box in local space (for TrafficSense casts).</summary>
        public Vector3 HalfExtents => definition != null ? definition.bodySize * 0.5f : new Vector3(0.9f, 0.7f, 2.3f);

        Rigidbody _rb;
        DriveInput _input;
        readonly PedalLogic _pedals = new PedalLogic();
        float _steerAngle;
        int _groundedWheels;
        float _rearStiffness = 1f;
        Vector3 _kinematicVelocity;
        Vector3 _lastKinematicPos;
        bool _warnedNoDef;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            Seat = GetComponentInChildren<VehicleSeat>(true);
            ApplyDefinition();
        }

        void OnEnable()
        {
            if (Id == 0) Id = _nextId++;
            if (!_all.Contains(this)) _all.Add(this);
            TrafficSense.Register(this);
        }

        void OnDisable()
        {
            _all.Remove(this);
            TrafficSense.Unregister(this);
        }

        /// <summary>Copies the definition onto the Rigidbody and WheelColliders.</summary>
        public void ApplyDefinition()
        {
            if (definition == null)
            {
                if (!_warnedNoDef) { _warnedNoDef = true; Debug.LogWarning("[VehicleController] No VehicleDefinition on " + name, this); }
                return;
            }
            _rb.mass = definition.massKg;
            _rb.centerOfMass = definition.centerOfMass;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            VehicleRb.SetDrag(_rb, 0.02f, 0.5f);

            foreach (var w in Wheels())
            {
                if (w.collider == null) continue;
                var wc = w.collider;
                wc.radius = definition.wheelRadius;
                wc.mass = 20f;
                wc.suspensionDistance = definition.suspensionDistance;
                wc.forceAppPointDistance = 0.1f; // lower roll moment, more planted
                float sprung = definition.massKg / 4f;
                float omega = 2f * Mathf.PI * definition.suspensionFrequencyHz;
                float k = sprung * omega * omega;
                var spring = wc.suspensionSpring;
                spring.spring = k;
                spring.damper = 2f * definition.suspensionDampingRatio * Mathf.Sqrt(k * sprung);
                spring.targetPosition = 0.5f;
                wc.suspensionSpring = spring;
            }
        }

        IEnumerable<Wheel> Wheels()
        {
            yield return frontLeft; yield return frontRight; yield return rearLeft; yield return rearRight;
        }

        // ------------------------------------------------------------------
        // Driver API
        // ------------------------------------------------------------------

        public void SetInput(DriveInput input) { _input = input.Clamped(); }

        public void SetDriver(VehicleDriver driver)
        {
            Driver = driver;
            if (driver == VehicleDriver.None) Park();
        }

        /// <summary>No input, handbrake on, pedals reset. Parked drivables sit like this.</summary>
        public void Park()
        {
            _input = new DriveInput { Handbrake = true, Brake = 1f };
            _pedals.Reset();
        }

        /// <summary>
        /// Far-away bots run kinematic to save CPU on WebGL (spec 8.3). Never used
        /// for the player's car. WheelColliders are switched off while kinematic.
        /// </summary>
        public void SetKinematic(bool kinematic)
        {
            if (_rb.isKinematic == kinematic) return;
            if (kinematic)
            {
                _kinematicVelocity = VehicleRb.GetVelocity(_rb);
                VehicleRb.SetVelocity(_rb, Vector3.zero);
                _rb.angularVelocity = Vector3.zero;
            }
            _rb.isKinematic = kinematic;
            foreach (var w in Wheels()) if (w.collider != null) w.collider.enabled = !kinematic;
            if (!kinematic) VehicleRb.SetVelocity(_rb, _kinematicVelocity);
            _lastKinematicPos = _rb.position;
        }

        /// <summary>Kinematic step for far bots: call from FixedUpdate only.</summary>
        public void KinematicMove(Vector3 position, Quaternion rotation, float dt)
        {
            if (!_rb.isKinematic) return;
            if (dt > 0f) _kinematicVelocity = (position - _lastKinematicPos) / dt;
            _lastKinematicPos = position;
            _rb.MovePosition(position);
            _rb.MoveRotation(rotation);
        }

        // ------------------------------------------------------------------
        // Physics
        // ------------------------------------------------------------------

        void FixedUpdate()
        {
            if (definition == null || _rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            float fwd = ForwardSpeed;
            float speed = Mathf.Abs(fwd);

            // Pedals -> motor and brake shares (one pedal pair, spec 6).
            _pedals.Step(_input.Throttle, _input.Brake, fwd, dt, out float motor, out float brake);
            if (!EngineOn) motor = 0f;

            float top = motor >= 0f ? definition.TopSpeedMs : definition.ReverseTopSpeedMs;
            float torque = motor * definition.MotorTorque * VehicleMath.TorqueFactor(fwd, top);
            // Do not push past top speed in either direction.
            if ((motor > 0f && fwd >= top) || (motor < 0f && -fwd >= top)) torque = 0f;

            bool rearDrive = definition.driveAxle == DriveAxle.Rear;
            SetMotor(rearDrive ? rearLeft : frontLeft, torque * 0.5f);
            SetMotor(rearDrive ? rearRight : frontRight, torque * 0.5f);
            SetMotor(rearDrive ? frontLeft : rearLeft, 0f);
            SetMotor(rearDrive ? frontRight : rearRight, 0f);

            float brakePerWheel = brake * definition.BrakeTorque * 0.25f;
            SetBrake(frontLeft, brakePerWheel * 1.2f);
            SetBrake(frontRight, brakePerWheel * 1.2f);
            float rearBrake = _input.Handbrake ? definition.BrakeTorque * 0.5f : brakePerWheel * 0.8f;
            SetBrake(rearLeft, rearBrake);
            SetBrake(rearRight, rearBrake);

            // Handbrake loosens the rear grip (spec 6).
            float wantRear = _input.Handbrake ? definition.handbrakeRearStiffness : 1f;
            if (!Mathf.Approximately(wantRear, _rearStiffness))
            {
                _rearStiffness = Mathf.MoveTowards(_rearStiffness, wantRear, 4f * dt);
                SetSideStiffness(rearLeft, _rearStiffness);
                SetSideStiffness(rearRight, _rearStiffness);
            }

            // Speed-sensitive steering, eased (no instant keyboard snaps).
            float maxSteer = VehicleMath.MaxSteerAngle(speed, definition.TopSpeedMs,
                definition.maxSteerLowSpeedDeg, definition.maxSteerTopSpeedDeg);
            _steerAngle = Mathf.MoveTowards(_steerAngle, _input.Steer * maxSteer, definition.steerSpeedDegPerSec * dt);
            if (frontLeft.collider != null) frontLeft.collider.steerAngle = _steerAngle;
            if (frontRight.collider != null) frontRight.collider.steerAngle = _steerAngle;

            // Stability: anti-roll, downforce, yaw assist.
            _groundedWheels = 0;
            AntiRoll(frontLeft.collider, frontRight.collider);
            AntiRoll(rearLeft.collider, rearRight.collider);
            _rb.AddForce(-transform.up * definition.downforce * speed * speed);
            if (Mathf.Abs(_input.Steer) < 0.05f && _groundedWheels >= 3 && !_input.Handbrake)
            {
                Vector3 av = _rb.angularVelocity;
                av.y *= Mathf.Clamp01(1f - definition.yawAssist * dt);
                _rb.angularVelocity = av;
            }
        }

        static void SetMotor(Wheel w, float t) { if (w.collider != null) w.collider.motorTorque = t; }
        static void SetBrake(Wheel w, float t) { if (w.collider != null) w.collider.brakeTorque = t; }

        static void SetSideStiffness(Wheel w, float s)
        {
            if (w.collider == null) return;
            var f = w.collider.sidewaysFriction;
            f.stiffness = s;
            w.collider.sidewaysFriction = f;
        }

        void AntiRoll(WheelCollider left, WheelCollider right)
        {
            if (left == null || right == null) return;
            float travelL = 1f, travelR = 1f;
            bool gl = left.GetGroundHit(out WheelHit hitL);
            bool gr = right.GetGroundHit(out WheelHit hitR);
            if (gl) { _groundedWheels++; travelL = (-left.transform.InverseTransformPoint(hitL.point).y - left.radius) / left.suspensionDistance; }
            if (gr) { _groundedWheels++; travelR = (-right.transform.InverseTransformPoint(hitR.point).y - right.radius) / right.suspensionDistance; }
            // travel: 0 = compressed, 1 = extended (a wheel off the ground counts as extended).
            float force = VehicleMath.AntiRollForce(travelL, travelR, definition.antiRollStiffness);
            if (gl) _rb.AddForceAtPosition(left.transform.up * -force, left.transform.position);
            if (gr) _rb.AddForceAtPosition(right.transform.up * force, right.transform.position);
        }

        void Update()
        {
            SyncVisual(frontLeft); SyncVisual(frontRight); SyncVisual(rearLeft); SyncVisual(rearRight);
        }

        void SyncVisual(Wheel w)
        {
            if (w.collider == null || w.visual == null) return;
            if (_rb.isKinematic)
            {
                // Spin visually at the mover's speed.
                float deg = ForwardSpeed / Mathf.Max(0.1f, w.collider.radius) * Mathf.Rad2Deg * Time.deltaTime;
                w.visual.Rotate(deg, 0f, 0f, Space.Self);
                return;
            }
            w.collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            w.visual.SetPositionAndRotation(pos, rot);
        }
    }

    /// <summary>
    /// Unity 6 renamed Rigidbody.velocity to linearVelocity (and drag to
    /// linearDamping). One place to switch, so the rest compiles on both.
    /// </summary>
    public static class VehicleRb
    {
        public static Vector3 GetVelocity(Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDrag(Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }
    }
}
