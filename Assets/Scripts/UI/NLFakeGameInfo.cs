// Naija Streets (Unity rebuild) — NLFakeGameInfo
// Demo feed for the HUD verify scene and the M1 greybox until the real
// game supplies live data. Oshodi, morning, healthy vitals, one objective.

using System;

namespace NaijaLife.UI
{
    public class NLFakeGameInfo : INLGameInfo
    {
        public string PlaceName => "OSHODI";
        public string ClockText => "07:42";
        public string DayText => "TUESDAY, WAT";
        public float Hunger01 => 0.8f;
        public float Energy01 => 0.65f;
        public float Pawa01 => 0.9f;
        public int WantedLevel => 0;
        public string ObjectiveText => "Find Musa at the park";
        public string RideText => null;
        public string NextTitle => "Next: find Musa";
        public string NextSub => "Oshodi park, 120 m";
        public float PlayerHeadingDegrees => 0f;

        public event Action OnChanged;
        public void NotifyChanged() => OnChanged?.Invoke();
    }
}
