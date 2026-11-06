// Naija Streets (Unity) - IJobUi on the HUD workstream's UI Toolkit HUD:
// prompts go to NLPrompts (the one-prompt rule) and toasts to NLToasts.
// The shout ring, change sheet, shift chip, busy line and summary card draw
// through NLJobHud (HUD-8). JobDebugUi is only the fallback when a scene has
// NLPrompts but no NLJobHud (the IJobUi contract is in IJobUi.cs).
// PlayerJobs uses this automatically when an NLPrompts is in the scene.

using System;
using NaijaLife.UI;
using UnityEngine;

namespace NaijaLife.JobsGlue
{
    public sealed class NLJobUiBridge : IJobUi
    {
        readonly JobDebugUi _rest;

        public NLJobUiBridge(JobDebugUi rest) { _rest = rest; }

        public static bool HudPresent => NLPrompts.Instance != null;

        public void Offer(string id, PromptKind kind, string verb, string subject, string line, Action onAccept)
        {
            NLPrompts.Offer(new NLPromptOffer
            {
                Id = id,
                Kind = kind == PromptKind.Exit ? NLPromptKind.Exit : kind == PromptKind.Other ? NLPromptKind.Other : NLPromptKind.Action,
                Verb = verb,
                Subject = subject,
                Icon = kind == PromptKind.Exit ? "door" : "briefcase",
                OnAccept = onAccept,
                Explain = line,
            });
        }

        public void Withdraw(string id) => NLPrompts.Withdraw(id);

        public void Toast(string title, string line) => NLToasts.Show("job." + title, NLToastKind.Job, title, line);

        static NLJobHud Hud => NLJobHud.Instance;

        public void ShowShiftChip(bool show, float secondsLeft, int trips, long pending, string note)
        {
            if (Hud != null) Hud.ShiftChip(show, secondsLeft, trips, pending, note); else _rest.ShowShiftChip(show, secondsLeft, trips, pending, note);
        }

        public void ShowShout(ShoutView? view, Action onShout)
        {
            if (Hud == null) { _rest.ShowShout(view, onShout); return; }
            if (!view.HasValue) { Hud.HideShout(); return; }
            ShoutView v = view.Value;
            Hud.Shout(v.RingScale, v.Gold, v.Seats, v.SeatsMax, v.SecondsLeft, v.Say, onShout ?? (() => { }));
        }

        public void ShowChange(ChangeView? view, Action<int> onPick)
        {
            if (Hud == null) { _rest.ShowChange(view, onPick); return; }
            if (!view.HasValue) { Hud.Change(true, 0, 0, 0, 0, null, 0f, null, null); return; }
            ChangeView c = view.Value;
            Hud.Change(false, c.Index, c.Of, c.Note, c.Fare, c.Options, c.SecondsLeft, c.Result, onPick);
        }

        public void ShowBusy(string text)
        {
            if (Hud != null) Hud.Busy(text); else _rest.ShowBusy(text);
        }

        public void ShowSummary(string title, string body, Action onClose)
        {
            if (Hud != null) Hud.Summary(title, body, onClose); else _rest.ShowSummary(title, body, onClose);
        }
    }
}
