// Naija Streets (Unity rebuild) — NLPromptManager
// The ONLY code that shows an action prompt. Spec section 6.
// Offer / Withdraw / Accept. Kinds: action, exit, other. Priority:
// exit > action > other, then nearest anchor. Hysteresis: an offer shows
// at 2.5 m and hides past 3.0 m; the visible prompt cannot change more than
// once per 0.4 s. 2+ live offers show a "+N" chip; tapping it (or Tab)
// opens a chooser sheet listing all offers as full-width 52 px rows.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public enum PromptKind { Action, Exit, Other }

    public struct PromptOffer
    {
        public string Id;
        public PromptKind Kind;
        public string Verb;      // "Take over"
        public string Subject;   // "Danfo conductor"
        public string Icon;      // icon name, e.g. "bus"
        public Vector3 WorldAnchor;
        public Action OnAccept;
    }

    public class NLPromptManager
    {
        private const float ShowDistance = 2.5f;
        private const float HideDistance = 3.0f;
        private const float MinSwapSeconds = 0.4f;

        private readonly VisualElement _panel;
        private readonly Label _verbLabel;
        private readonly Label _subjectLabel;
        private readonly Label _keyLabel;
        private readonly VisualElement _iconEl;
        private readonly Button _moreChip;
        private readonly Label _moreLabel;
        private readonly VisualElement _chooserSheet;
        private readonly VisualElement _chooserList;
        private readonly Func<Vector3> _playerPosition;

        private readonly Dictionary<string, PromptOffer> _offers = new Dictionary<string, PromptOffer>();
        private string _visibleId;
        private float _lastSwapTime = -10f;
        private bool _chooserOpen;

        public string VisibleId => _visibleId;
        public int LiveCount => _offers.Count;
        public bool ChooserOpen => _chooserOpen;

        public NLPromptManager(VisualElement panel, VisualElement chooserSheet, Func<Vector3> playerPosition)
        {
            _panel = panel;
            _chooserSheet = chooserSheet;
            _playerPosition = playerPosition;
            _verbLabel = panel.Q<Label>("nl-prompt-verb");
            _subjectLabel = panel.Q<Label>("nl-prompt-subject");
            _keyLabel = panel.Q<Label>("nl-prompt-key");
            _iconEl = panel.Q<VisualElement>("nl-prompt-icon");
            _moreChip = panel.Q<Button>("nl-prompt-more");
            _moreLabel = panel.Q<Label>("nl-prompt-more-label");
            _chooserList = chooserSheet?.Q<VisualElement>("nl-chooser-list");
            _panel.style.display = DisplayStyle.None;
            if (_chooserSheet != null) _chooserSheet.style.display = DisplayStyle.None;
            if (_moreChip != null) _moreChip.clicked += ToggleChooser;
            if (_panel is Button panelBtn) panelBtn.clicked += Accept;
            else _panel.RegisterCallback<ClickEvent>(_ => Accept());
        }

        public void Offer(PromptOffer o)
        {
            _offers[o.Id] = o;
            Evaluate(true);
        }

        public void Withdraw(string id)
        {
            if (!_offers.Remove(id)) return;
            if (_visibleId == id) _visibleId = null;
            Evaluate(true);
        }

        public void Accept()
        {
            if (_chooserOpen) { CloseChooser(); return; }
            if (_visibleId == null) return;
            if (!_offers.TryGetValue(_visibleId, out var offer)) return;
            offer.OnAccept?.Invoke();
        }

        public void ToggleChooser()
        {
            if (_chooserOpen) CloseChooser();
            else OpenChooser();
        }

        public void OpenChooser()
        {
            if (_offers.Count < 2 || _chooserSheet == null) return;
            _chooserOpen = true;
            RebuildChooser();
            _chooserSheet.style.display = DisplayStyle.Flex;
        }

        public void CloseChooser()
        {
            _chooserOpen = false;
            if (_chooserSheet != null) _chooserSheet.style.display = DisplayStyle.None;
        }

        /// <summary>Call every frame (or on offer changes) to apply hysteresis and swaps.</summary>
        public void Tick()
        {
            Evaluate(false);
        }

        private void Evaluate(bool immediate)
        {
            Vector3 playerPos = _playerPosition != null ? _playerPosition() : Vector3.zero;
            PromptOffer? best = null;
            foreach (var o in _offers.Values)
            {
                float d = Vector3.Distance(playerPos, o.WorldAnchor);
                if (d > HideDistance) continue;
                if (best == null || Better(o, best.Value, playerPos)) best = o;
            }

            string bestId = best.HasValue ? best.Value.Id : null;
            if (bestId == _visibleId)
            {
                UpdateMoreChip();
                return;
            }
            // Hysteresis: the current prompt stays until its anchor is past 3.0 m,
            // and the visible prompt cannot change more than once per 0.4 s.
            if (_visibleId != null && _offers.TryGetValue(_visibleId, out var cur))
            {
                float curD = Vector3.Distance(playerPos, cur.WorldAnchor);
                if (curD <= HideDistance && Time.time - _lastSwapTime < MinSwapSeconds && !immediate)
                    return;
            }
            if (!immediate && Time.time - _lastSwapTime < MinSwapSeconds && _visibleId != null && bestId != null)
                return;

            _visibleId = bestId;
            _lastSwapTime = Time.time;
            if (best.HasValue) Show(best.Value);
            else Hide();
            UpdateMoreChip();
        }

        private bool Better(PromptOffer a, PromptOffer b, Vector3 playerPos)
        {
            int pa = Priority(a.Kind), pb = Priority(b.Kind);
            if (pa != pb) return pa > pb;
            return Vector3.Distance(playerPos, a.WorldAnchor) < Vector3.Distance(playerPos, b.WorldAnchor);
        }

        private static int Priority(PromptKind kind)
        {
            switch (kind)
            {
                case PromptKind.Exit: return 3;
                case PromptKind.Action: return 2;
                default: return 1;
            }
        }

        private void Show(PromptOffer o)
        {
            if (_verbLabel != null) _verbLabel.text = o.Verb;
            if (_subjectLabel != null) _subjectLabel.text = o.Subject;
            if (_iconEl != null) NLIcon.Set(_iconEl, o.Icon);
            if (_keyLabel != null) _keyLabel.text = "E";
            _panel.style.display = DisplayStyle.Flex;
            _panel.RemoveFromClassList("nl-prompt--in");
            // Entrance class on the next frame so the USS transition runs.
            _panel.schedule.Execute(() => _panel.AddToClassList("nl-prompt--in"));
        }

        private void Hide()
        {
            _panel.style.display = DisplayStyle.None;
            CloseChooser();
        }

        private void UpdateMoreChip()
        {
            int extra = Math.Max(0, _offers.Count - 1);
            if (_moreChip == null) return;
            _moreChip.style.display = extra > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_moreLabel != null) _moreLabel.text = string.Format(NLStrings.PromptMore, extra);
        }

        private void RebuildChooser()
        {
            if (_chooserList == null) return;
            _chooserList.Clear();
            Vector3 playerPos = _playerPosition != null ? _playerPosition() : Vector3.zero;
            var sorted = new List<PromptOffer>(_offers.Values);
            sorted.Sort((a, b) =>
            {
                int pa = Priority(a.Kind), pb = Priority(b.Kind);
                if (pa != pb) return pb.CompareTo(pa);
                return Vector3.Distance(playerPos, a.WorldAnchor)
                    .CompareTo(Vector3.Distance(playerPos, b.WorldAnchor));
            });
            foreach (var o in sorted)
            {
                var row = new Button(() => { CloseChooser(); o.OnAccept?.Invoke(); });
                row.AddToClassList("nl-chooser-row");
                var icon = new VisualElement();
                icon.AddToClassList("nl-icon-tile");
                var iconImg = new VisualElement();
                iconImg.AddToClassList("nl-icon");
                NLIcon.Set(iconImg, o.Icon);
                icon.Add(iconImg);
                var verb = new Label(o.Verb) { name = "nl-chooser-verb" };
                verb.AddToClassList("nl-text-value");
                var subject = new Label(o.Subject) { name = "nl-chooser-subject" };
                subject.AddToClassList("nl-text-body");
                var texts = new VisualElement();
                texts.AddToClassList("nl-chooser-texts");
                texts.Add(verb);
                texts.Add(subject);
                row.Add(icon);
                row.Add(texts);
                _chooserList.Add(row);
            }
        }
    }
}
