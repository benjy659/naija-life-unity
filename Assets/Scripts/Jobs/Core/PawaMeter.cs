// Naija Streets (Unity) - Pawa, the stamina for work (docs/specs/jobs-m4.md section 7).

using System;

namespace NaijaLife.Jobs
{
    public sealed class PawaMeter
    {
        public float Value { get; private set; }

        public PawaMeter(float start = 100f) { Value = Clamp(start); }

        public bool CanClockIn => Value >= ShiftRules.PawaToClockIn;

        /// <summary>Whole Pawa the server sees (it checks 25..100).</summary>
        public int Reported => (int)Math.Floor(Value);

        public void DrainWorking(float seconds) { if (seconds > 0f) Value = Clamp(Value - ShiftRules.PawaDrainPerMinute * seconds / 60f); }

        public void Add(float amount) { Value = Clamp(Value + amount); }

        public void Set(float v) { Value = Clamp(v); }

        public string RefuseText => "You too tire. Chop first. (Pawa " + Reported + "/" + ShiftRules.PawaToClockIn + ")";

        static float Clamp(float v) => v < 0f ? 0f : (v > 100f ? 100f : v);
    }
}
