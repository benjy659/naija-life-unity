// M3 (docs/specs/vehicles-m3.md section 3, 9). The ONLY way anything drives a
// vehicle: player input, bot AI and later the network all produce a DriveInput.
// Pure data, no UnityEngine, so it lives in the Logic assembly and is testable.

namespace NaijaLife.Vehicles
{
    public struct DriveInput
    {
        /// <summary>Gas pedal, 0..1.</summary>
        public float Throttle;
        /// <summary>Brake pedal, 0..1. Held at a stop it engages reverse (PedalLogic).</summary>
        public float Brake;
        /// <summary>Steering, -1 (full left) .. 1 (full right).</summary>
        public float Steer;
        public bool Handbrake;

        public static DriveInput None => default;

        public DriveInput Clamped()
        {
            return new DriveInput
            {
                Throttle = VehicleMath.Clamp01(Throttle),
                Brake = VehicleMath.Clamp01(Brake),
                Steer = VehicleMath.Clamp(Steer, -1f, 1f),
                Handbrake = Handbrake
            };
        }
    }
}
