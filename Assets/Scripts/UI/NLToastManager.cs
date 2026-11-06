// Naija Streets (Unity rebuild) — NLToastManager
// Spec section 7. Show(key, kind, title, line, icon). 3 live (2 in
// portrait), 5 queued, newest at the top of the lane. Same key within 10 s
// updates the live toast instead of adding one (money toasts add up).
// 3.5 s on screen, slide in from the right, fade out. Tap dismisses.
// Kinds: money (coin), job (briefcase), police (siren), people (people),
// city (gov), system (warn). A kind sets only the icon and the left bar
// colour (accent, danger for police, live for people). Never centred, never
// modal.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public enum ToastKind { Money, Job, Police, People, City, System }

    public class NLToastManager
    {
        private const float LiveSeconds = 3.5f;
        private const float DedupeSeconds = 10f;
        private const int MaxLive = 3;
        private const int MaxLivePortrait = 2;
        private const int MaxQueued = 5;

        private class Toast
        {
            public string Key;
            public ToastKind Kind;
            public string Title;
            public string Line;
            public string Icon;
            public long Amount;       // money toasts accumulate
            public bool HasAmount;
            public float ShownAt;
            public float KeyAt;
            public VisualElement Element;
            public Label TitleLabel;
            public Label LineLabel;
        }

        private readonly VisualElement _lane;
        private readonly List<Toast> _live = new List<Toast>();
        private readonly Queue<Toast> _queue = new Queue<Toast>();
        private readonly Dictionary<string, Toast> _byKey = new Dictionary<string, Toast>();
        private bool _portrait;

        public int LiveCount => _live.Count;
        public int QueuedCount => _queue.Count;

        public NLToastManager(VisualElement lane)
        {
            _lane = lane;
        }

        public void SetPortrait(bool portrait) => _portrait = portrait;

        public void Show(string key, ToastKind kind, string title, string line, string icon, long? amount = null)
        {
            if (_byKey.TryGetValue(key, out var existing) && Time.time - existing.KeyAt < DedupeSeconds)
            {
                // Same key within 10 s: update the live toast instead of adding one.
                if (amount.HasValue && existing.HasAmount)
                {
                    existing.Amount += amount.Value;
                    existing.Title = MoneyTitle(existing.Amount);
                }
                else
                {
                    existing.Title = title;
                    existing.Line = line;
                }
                existing.KeyAt = Time.time;
                existing.ShownAt = Time.time;
                if (existing.TitleLabel != null) existing.TitleLabel.text = existing.Title;
                if (existing.LineLabel != null) existing.LineLabel.text = existing.Line;
                return;
            }

            var toast = new Toast
            {
                Key = key, Kind = kind, Title = title, Line = line, Icon = icon,
                Amount = amount ?? 0, HasAmount = amount.HasValue,
                ShownAt = Time.time, KeyAt = Time.time
            };
            if (toast.HasAmount) toast.Title = MoneyTitle(toast.Amount);
            _byKey[key] = toast;

            int cap = _portrait ? MaxLivePortrait : MaxLive;
            if (_live.Count >= cap)
            {
                if (_queue.Count >= MaxQueued) _queue.Dequeue();
                _queue.Enqueue(toast);
                return;
            }
            AddLive(toast);
        }

        private string MoneyTitle(long amount)
        {
            return (amount >= 0 ? "+" : "") + NLMoney.Short(amount);
        }

        private void AddLive(Toast toast)
        {
            var el = new VisualElement();
            el.AddToClassList("nl-toast");
            el.AddToClassList("nl-toast--" + toast.Kind.ToString().ToLowerInvariant());
            var bar = new VisualElement();
            bar.AddToClassList("nl-toast-bar");
            var iconWrap = new VisualElement();
            iconWrap.AddToClassList("nl-icon-tile");
            var icon = new VisualElement();
            icon.AddToClassList("nl-icon");
            NLIcon.Set(icon, toast.Icon);
            iconWrap.Add(icon);
            var texts = new VisualElement();
            texts.AddToClassList("nl-toast-texts");
            var title = new Label(toast.Title);
            title.AddToClassList("nl-text-value");
            var line = new Label(toast.Line);
            line.AddToClassList("nl-text-body");
            texts.Add(title);
            texts.Add(line);
            el.Add(bar);
            el.Add(iconWrap);
            el.Add(texts);
            el.RegisterCallback<ClickEvent>(_ => Dismiss(toast));
            toast.Element = el;
            toast.TitleLabel = title;
            toast.LineLabel = line;
            _live.Add(toast);
            _lane.Insert(0, el);
            // Entrance class on the next frame so the USS transition runs.
            el.schedule.Execute(() => el.AddToClassList("nl-toast--in"));
        }

        private void Dismiss(Toast toast)
        {
            if (toast.Element != null) toast.Element.RemoveFromHierarchy();
            _live.Remove(toast);
            PromoteQueued();
        }

        /// <summary>Call every frame: expires old toasts and promotes the queue.</summary>
        public void Tick()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (Time.time - _live[i].ShownAt >= LiveSeconds)
                    Dismiss(_live[i]);
            }
        }

        private void PromoteQueued()
        {
            int cap = _portrait ? MaxLivePortrait : MaxLive;
            while (_live.Count < cap && _queue.Count > 0)
                AddLive(_queue.Dequeue());
        }

        /// <summary>NLScreenGuard hides the lane (toasts stay queued) when it would cover the player.</summary>
        public void SetLaneHidden(bool hidden)
        {
            if (_lane != null) _lane.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
