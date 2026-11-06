// M3 per-vehicle numbers (docs/specs/vehicles-m3.md section 4). One asset per
// type in Assets/Settings/Vehicles/. Create via Assets > Create > Naija > Vehicle
// Definition, or let the Greybox_M3 builder (Naija Streets > M3 > Build test scene)
// create Danfo.asset and TokunboSaloon.asset with the spec values.

using UnityEngine;

namespace NaijaLife.Vehicles
{
    public enum DriveAxle { Rear, Front }

    [CreateAssetMenu(menuName = "Naija/Vehicle Definition", fileName = "VehicleDefinition")]
    public sealed class VehicleDefinition : ScriptableObject
    {
        [Header("Identity (player-facing names: fictional, never a real brand)")]
        public string displayName = "Tokunbo saloon";
        [Tooltip("Stable id for netcode and saves (NetPose.VehicleModelId).")]
        public int modelId = 2;

        [Header("Size (m)")]
        public Vector3 bodySize = new Vector3(1.8f, 1.45f, 4.6f);
        public float wheelbase = 2.7f;
        public float track = 1.52f;
        public float wheelRadius = 0.31f;

        [Header("Mass")]
        public float massKg = 1300f;
        public Vector3 centerOfMass = new Vector3(0f, 0.40f, 0.05f);

        [Header("Speed")]
        public float topSpeedKmh = 140f;
        public float reverseTopSpeedKmh = 20f;
        [Tooltip("Seconds from 0 to 50 km/h on the flat. Motor torque is derived from it.")]
        public float zeroTo50Seconds = 4.5f;
        [Tooltip("Extra torque share to beat rolling resistance and drag (1 = none).")]
        public float torqueHeadroom = 1.35f;
        public DriveAxle driveAxle = DriveAxle.Front;

        [Header("Steering")]
        public float maxSteerLowSpeedDeg = 35f;
        public float maxSteerTopSpeedDeg = 8f;
        [Tooltip("Degrees per second the front wheels turn toward the target angle.")]
        public float steerSpeedDegPerSec = 120f;

        [Header("Brakes")]
        public float brakeDecel = 7.5f;
        [Tooltip("Rear sideways friction stiffness while the handbrake is held.")]
        public float handbrakeRearStiffness = 0.5f;

        [Header("Stability")]
        public float antiRollStiffness = 6500f;
        [Tooltip("Downforce coefficient: F = k * speed^2 (N).")]
        public float downforce = 0.5f;
        [Tooltip("Spin damping when no steer input is held (0..1 per second).")]
        public float yawAssist = 0.6f;
        public float suspensionDistance = 0.2f;
        public float suspensionFrequencyHz = 1.5f;
        [Range(0.1f, 1f)] public float suspensionDampingRatio = 0.35f;

        [Header("Camera (PlayerCamera.SetDrivingMode)")]
        public float cameraDistance = 6.5f;
        public float cameraPivotHeight = 1.8f;

        [Header("Seats and damage")]
        public int passengerSeats = 4;
        [Tooltip("Impulse (N s) below which a bump does no damage.")]
        public float damageThreshold = 1500f;
        [Tooltip("Impulse per damage point.")]
        public float damageScale = 400f;

        public float TopSpeedMs => topSpeedKmh / VehicleMath.KmhPerMs;
        public float ReverseTopSpeedMs => reverseTopSpeedKmh / VehicleMath.KmhPerMs;

        /// <summary>Total motor torque at the driven axle (N m).</summary>
        public float MotorTorque => VehicleMath.TorqueFor0To50(massKg, zeroTo50Seconds, wheelRadius) * torqueHeadroom;

        /// <summary>Total brake torque over four wheels (N m).</summary>
        public float BrakeTorque => VehicleMath.BrakeTorqueFor(massKg, brakeDecel, wheelRadius);

        // ---- Spec section 4 presets (used by the test-scene builder) ----

        public void ApplyDanfoPreset()
        {
            displayName = "Danfo";
            modelId = 1;
            bodySize = new Vector3(1.9f, 2.3f, 4.9f);
            wheelbase = 2.46f; track = 1.57f; wheelRadius = 0.33f;
            massKg = 1800f; centerOfMass = new Vector3(0f, 0.55f, 0.1f);
            topSpeedKmh = 90f; reverseTopSpeedKmh = 15f; zeroTo50Seconds = 7f;
            driveAxle = DriveAxle.Rear;
            maxSteerLowSpeedDeg = 38f; maxSteerTopSpeedDeg = 10f; steerSpeedDegPerSec = 90f;
            brakeDecel = 6f; antiRollStiffness = 9000f; suspensionDistance = 0.25f;
            cameraDistance = 8f; cameraPivotHeight = 2.6f;
            passengerSeats = 14;
            damageThreshold = 2000f; damageScale = 500f;
        }

        public void ApplySaloonPreset()
        {
            displayName = "Tokunbo saloon";
            modelId = 2;
            bodySize = new Vector3(1.8f, 1.45f, 4.6f);
            wheelbase = 2.7f; track = 1.52f; wheelRadius = 0.31f;
            massKg = 1300f; centerOfMass = new Vector3(0f, 0.40f, 0.05f);
            topSpeedKmh = 140f; reverseTopSpeedKmh = 20f; zeroTo50Seconds = 4.5f;
            driveAxle = DriveAxle.Front;
            maxSteerLowSpeedDeg = 35f; maxSteerTopSpeedDeg = 8f; steerSpeedDegPerSec = 120f;
            brakeDecel = 7.5f; antiRollStiffness = 6500f; suspensionDistance = 0.2f;
            cameraDistance = 6.5f; cameraPivotHeight = 1.8f;
            passengerSeats = 4;
            damageThreshold = 1500f; damageScale = 400f;
        }
    }
}
