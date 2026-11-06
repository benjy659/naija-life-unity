// Naija Streets (Unity) - what the jobs code needs from the HUD. The HUD
// workstream implements this with NLPromptManager / NLToastManager
// (docs/specs/hud-and-phone.md sections 6 and 7). Until then JobDebugUi draws
// a plain placeholder so M4 can be played and checked.

using System;

namespace NaijaLife.JobsGlue
{
    public enum PromptKind { Action, Exit, Other }

    public interface IJobUi
    {
        /// <summary>One prompt (the one-prompt rule). Same id updates it.</summary>
        void Offer(string id, PromptKind kind, string verb, string subject, string line, Action onAccept);
        void Withdraw(string id);
        void Toast(string title, string line);

        /// <summary>Shift chip: time left, trips, earned this shift (pending, not in the wallet yet).</summary>
        void ShowShiftChip(bool show, float secondsLeft, int trips, long pending, string note);

        /// <summary>Shout ring: scale 2..0.4, gold flag, seats filled, seconds left. Null hides it.</summary>
        void ShowShout(ShoutView? view, Action onShout);

        /// <summary>Change sheet: one fare. Null hides it.</summary>
        void ShowChange(ChangeView? view, Action<int> onPick);

        /// <summary>Busy state ("Saving your pay..."). Null hides it.</summary>
        void ShowBusy(string text);

        /// <summary>End-of-shift card.</summary>
        void ShowSummary(string title, string body, Action onClose);
    }

    public struct ShoutView { public float RingScale; public bool Gold; public int Seats; public int SeatsMax; public float SecondsLeft; public string Say; }
    public struct ChangeView { public int Index; public int Of; public int Note; public int Fare; public int[] Options; public float SecondsLeft; public string Result; }
}
