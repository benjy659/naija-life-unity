// Naija Streets (Unity rebuild) — INLGameInfo
// The HUD/phone read the live game through this interface. The M1 greybox
// and the verify scenes supply a fake; the real game supplies the live
// implementation once the M2 city and the M5/M6 economy land. Nothing in
// the UI layer reaches into gameplay classes directly.

using System;

namespace NaijaLife.UI
{
    public interface INLGameInfo
    {
        string PlaceName { get; }     // "OSHODI" — current area name
        string ClockText { get; }     // "07:42" — in-game time
        string DayText { get; }       // "TUESDAY, WAT"
        float Hunger01 { get; }       // 0-1
        float Energy01 { get; }       // 0-1
        float Pawa01 { get; }         // 0-1
        int WantedLevel { get; }      // 0-5, 0 = no wanted row
        string ObjectiveText { get; } // null/empty hides the objective chip
        string RideText { get; }      // null/empty hides the ride chip
        string NextTitle { get; }     // phone home "Next" card title
        string NextSub { get; }       // phone home "Next" card subtitle
        float PlayerHeadingDegrees { get; } // radar arrow rotation

        event Action OnChanged;
    }
}
