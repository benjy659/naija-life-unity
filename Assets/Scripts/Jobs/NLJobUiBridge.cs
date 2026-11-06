// Naija Streets (Unity) - IJobUi on the HUD workstream's UI Toolkit HUD:
// prompts go to NLPrompts (the one-prompt rule) and toasts to NLToasts.
// The shout ring, change sheet, shift chip and summary card are not in the
// HUD kit yet, so they still draw through JobDebugUi until the HUD thread
// builds them (ask in the inbox; the IJobUi contract is in IJobUi.cs).
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

        public void ShowShiftChip(bool show, float secondsLeft, int trips, long pending, string note) => _rest.ShowShiftChip(show, secondsLeft, trips, pending, note);
        public void ShowShout(ShoutView? view, Action onShout) => _rest.ShowShout(view, onShout);
        public void ShowChange(ChangeView? view, Action<int> onPick) => _rest.ShowChange(view, onPick);
        public void ShowBusy(string text) => _rest.ShowBusy(text);
        public void ShowSummary(string title, string body, Action onClose) => _rest.ShowSummary(title, body, onClose);
    }
}
