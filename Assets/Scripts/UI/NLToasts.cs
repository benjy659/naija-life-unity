// Naija Streets: toast lane (HUD-4). Spec docs/specs/hud-and-phone.md section 7.
//   - 3 live (2 in portrait), 5 queued, newest on top, right column only.
//   - Same key within 10 s updates the live toast; money toasts add up.
//   - 3.5 s on screen, slide in from the right, tap to dismiss. Never modal.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public enum NLToastKind { Money, Job, Police, People, City, System }

    public sealed class NLToasts : NLUIPart
    {
        public static NLToasts Instance { get; private set; }

        private const float LifeSeconds = 3.5f;
        private const float DedupeSeconds = 10f;
        private const int MaxQueued = 5;

        private sealed class Toast
        {
            public string Key; public NLToastKind Kind; public string Title, Line; public long Money;
            public VisualElement El; public Label TitleEl, LineEl; public float Until, Born;
        }

        private readonly List<Toast> _live = new List<Toast>();
        private readonly Queue<Toast> _queue = new Queue<Toast>();
        private readonly Dictionary<string, Toast> _recent = new Dictionary<string, Toast>();
        private VisualElement _lane;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Show a toast. key dedupes; addMoney (money kind) sums into the live toast.</summary>
        public static void Show(string key, NLToastKind kind, string title, string line, long addMoney = 0)
        {
            if (Instance == null) return;
            Instance.Push(new Toast { Key = key, Kind = kind, Title = title, Line = line, Money = addMoney });
        }

        protected override void OnBind(VisualElement root)
        {
            _lane = root.Q("nl-toasts");
            _lane.Clear();
            _live.Clear();
        }

        private int MaxLive => UI != null && UI.Mode == NLScreenMode.Portrait ? 2 : 3;

        private void Push(Toast t)
        {
            float now = Time.unscaledTime;
            if (t.Key != null && _recent.TryGetValue(t.Key, out var old) && now - old.Born < DedupeSeconds && old.El != null && _live.Contains(old))
            {
                if (t.Kind == NLToastKind.Money && t.Money != 0)
                {
                    old.Money += t.Money;
                    old.Title = NLMoney.Delta(old.Money);
                }
                else old.Title = t.Title;
                old.Line = t.Line;
                old.TitleEl.text = old.Title;
                old.LineEl.text = old.Line;
                old.Until = now + LifeSeconds;
                old.Born = now;
                return;
            }
            if (_queue.Count >= MaxQueued) _queue.Dequeue(); // drop the oldest waiting one, never spam
            _queue.Enqueue(t);
        }

        protected override void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (now >= _live[i].Until) Remove(_live[i]);

            while (_queue.Count > 0 && _live.Count < MaxLive && !NLPhone.IsOpen)
                Spawn(_queue.Dequeue(), now);
        }

        private void Spawn(Toast t, float now)
        {
            var el = new VisualElement();
            el.AddToClassList("nl-toast");
            if (t.Kind == NLToastKind.Police) el.AddToClassList("nl-toast--police");
            if (t.Kind == NLToastKind.People) el.AddToClassList("nl-toast--people");

            var tile = new VisualElement(); tile.AddToClassList("nl-icon-tile");
            var ico = new VisualElement(); ico.AddToClassList("nl-icon"); ico.AddToClassList("nl-ico-" + IconFor(t.Kind));
            tile.Add(ico);
            var text = new VisualElement(); text.AddToClassList("nl-toast-text");
            t.TitleEl = new Label(t.Title); t.TitleEl.AddToClassList("nl-toast-title");
            t.LineEl = new Label(t.Line); t.LineEl.AddToClassList("nl-toast-line");
            text.Add(t.TitleEl); text.Add(t.LineEl);
            el.Add(tile); el.Add(text);
            el.RegisterCallback<ClickEvent>(_ => Remove(t));

            t.El = el; t.Born = now; t.Until = now + LifeSeconds;
            _lane.Insert(0, el); // newest on top
            _live.Add(t);
            if (t.Key != null) _recent[t.Key] = t;
            SlideIn(el, null);
        }

        private void Remove(Toast t)
        {
            if (!_live.Remove(t) || t.El == null) return;
            var el = t.El;
            el.RemoveFromClassList("is-in");
            el.schedule.Execute(() => el.RemoveFromHierarchy()).StartingIn(NLUIRoot.ReducedMotion ? 0 : 230);
        }

        private static string IconFor(NLToastKind k)
        {
            switch (k)
            {
                case NLToastKind.Money: return "coin";
                case NLToastKind.Job: return "briefcase";
                case NLToastKind.Police: return "siren";
                case NLToastKind.People: return "people";
                case NLToastKind.City: return "gov";
                default: return "warn";
            }
        }
    }
}
