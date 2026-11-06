// Naija Streets: THE one action prompt (HUD-4). Spec docs/specs/hud-and-phone.md 6.
//   - Only NLPrompts shows an action prompt. Callers Offer and Withdraw.
//   - One slot under the radar. More offers show a "+N" chip; tapping it (or Tab)
//     cycles to the next offer (the bottom-sheet chooser comes with HUD-4 polish).
//   - Priority: exit > action > other, then the nearest anchor.
//   - Never flickers: the shown offer changes at most once per 0.4 s.
//     Callers own distance hysteresis (offer at 2.5 m, withdraw past 3.0 m).
//   - Accept: tap the prompt, E key, or gamepad South (A / Cross).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public enum NLPromptKind { Action = 1, Exit = 2, Other = 0 }

    public sealed class NLPromptOffer
    {
        public string Id;
        public NLPromptKind Kind = NLPromptKind.Action;
        public string Verb;          // "Take over"
        public string Subject;       // "Danfo conductor"
        public string Icon = "spark";// icon name from Assets/UI/Icons (ico-<name>.svg)
        public Transform Anchor;     // for nearest-first ordering; may be null
        public Action OnAccept;
        /// <summary>For Kind Other: one line shown when tapped, for example "Pawa too low. Chop first."</summary>
        public string Explain;
    }

    public sealed class NLPrompts : NLUIPart
    {
        public static NLPrompts Instance { get; private set; }

        private const float MinSwapSeconds = 0.4f;

        private readonly List<NLPromptOffer> _offers = new List<NLPromptOffer>();
        private NLPromptOffer _shown;
        private float _lastSwap = -10f;
        private int _cycle;
        private Transform _player;

        private VisualElement _el, _icon;
        private Label _verb, _subject, _more;
        private string _iconClass;

        // chooser sheet
        private VisualElement _chooserWrap, _chooser, _chooserList;
        private string _chooserKey;
        private int _chooserFrame = -1;
        /// <summary>True while the +N chooser sheet is open (Esc closes it, not the phone menu).</summary>
        public static bool ChooserOpen => Instance != null && Instance._chooser != null && Instance._chooser.ClassListContains("is-in");
        /// <summary>Frame the chooser last opened or closed, so one Esc press is never used twice.</summary>
        public static int ChooserFrame => Instance != null ? Instance._chooserFrame : -1;

        // keep-out fallback
        private bool _fallback;
        private Rect _homeRect;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Offer(NLPromptOffer o)
        {
            if (Instance == null || o == null || string.IsNullOrEmpty(o.Id)) return;
            Instance._offers.RemoveAll(x => x.Id == o.Id);
            Instance._offers.Add(o);
        }

        public static void Withdraw(string id)
        {
            if (Instance == null) return;
            Instance._offers.RemoveAll(x => x.Id == id);
        }

        public static bool IsShowing(string id) => Instance != null && Instance._shown != null && Instance._shown.Id == id;

        protected override void OnBind(VisualElement root)
        {
            _el = root.Q("nl-prompt");
            _icon = root.Q("nl-prompt-icon");
            _verb = root.Q<Label>("nl-prompt-verb");
            _subject = root.Q<Label>("nl-prompt-subject");
            _more = root.Q<Label>("nl-prompt-more");
            _el.AddToClassList("is-hidden");
            _el.RegisterCallback<ClickEvent>(e => { if (e.target == _more) return; Accept(); });
            _more.RegisterCallback<ClickEvent>(e => { OpenChooser(); e.StopPropagation(); });
            _shown = null;
            _fallback = false;

            _chooserWrap = new VisualElement { pickingMode = PickingMode.Ignore };
            _chooserWrap.AddToClassList("nl-chooser-wrap");
            _chooser = new VisualElement();
            _chooser.AddToClassList("nl-chooser");
            _chooser.AddToClassList("is-hidden");
            var head = new VisualElement(); head.AddToClassList("nl-chooser-head");
            var title = new Label("What you wan do?"); title.AddToClassList("nl-chooser-title"); head.Add(title);
            var close = new Button(CloseChooser) { text = "" };
            close.AddToClassList("nl-sq-btn"); close.AddToClassList("nl-sq-btn--ghost");
            NLPhone.Icon(close, "close");
            head.Add(close);
            _chooser.Add(head);
            _chooserList = new ScrollView(ScrollViewMode.Vertical);
            _chooserList.AddToClassList("nl-chooser-list");
            _chooser.Add(_chooserList);
            _chooserWrap.Add(_chooser);
            (root.Q("nl-safe") ?? root).Add(_chooserWrap);
            _chooserKey = null;
        }

        protected override void OnUnbind()
        {
            _chooserWrap?.RemoveFromHierarchy();
            _chooserWrap = null; _chooser = null; _chooserList = null;
        }

        protected override void Tick()
        {
            var best = Pick();
            if (best != _shown && Time.unscaledTime - _lastSwap >= MinSwapSeconds)
            {
                _lastSwap = Time.unscaledTime;
                Show(best);
            }
            if (_shown != null)
            {
                int others = _offers.Count - 1;
                _more.EnableInClassList("is-on", others > 0);
                if (others > 0) _more.text = "+" + others;
            }
            Guard();

            if (ChooserOpen)
            {
                if (_offers.Count < 2 || NLPhone.IsOpen) CloseChooser();
                else { RefreshChooser(); ChooserKeys(); return; }
            }

            if (NLPhone.IsOpen || NLJobHud.HoldsInput) return; // the job HUD owns E / South while its card or button is up
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) Accept();
            if (kb != null && kb.tabKey.wasPressedThisFrame && _chooserFrame != Time.frameCount) OpenChooser();
            var pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame && !NLJobHud.HoldsSouth) Accept();
        }

        private NLPromptOffer Pick()
        {
            if (_offers.Count == 0) return null;
            var me = Player();
            _offers.Sort((a, b) =>
            {
                int k = Rank(b.Kind).CompareTo(Rank(a.Kind));
                if (k != 0) return k;
                return Dist(a, me).CompareTo(Dist(b, me));
            });
            int i = Mathf.Clamp(_cycle, 0, _offers.Count - 1);
            if (_offers[0].Kind == NLPromptKind.Exit) i = 0; // a way out always wins
            return _offers[i];
        }

        private static int Rank(NLPromptKind k) => k == NLPromptKind.Exit ? 2 : (k == NLPromptKind.Action ? 1 : 0);

        private static float Dist(NLPromptOffer o, Transform me) =>
            o.Anchor == null || me == null ? 0f : (o.Anchor.position - me.position).sqrMagnitude;

        private Transform Player()
        {
            if (_player == null)
            {
                var pc = FindFirstObjectByType<NaijaLife.Player.PlayerController>();
                if (pc != null) _player = pc.transform;
            }
            return _player;
        }

        private void NextOffer()
        {
            if (_offers.Count < 2) return;
            _cycle = (_cycle + 1) % _offers.Count;
            _lastSwap = -10f; // explicit player choice: swap now
        }

        private void Show(NLPromptOffer o)
        {
            _shown = o;
            if (o == null)
            {
                _cycle = 0;
                SlideOut(_el, 230, null);
                _el.schedule.Execute(() => { if (_shown == null) _el.AddToClassList("is-hidden"); })
                   .StartingIn(NLUIRoot.ReducedMotion ? 0 : 230);
                return;
            }
            _verb.text = o.Verb ?? "";
            _subject.text = o.Subject ?? "";
            _subject.style.display = string.IsNullOrEmpty(o.Subject) ? DisplayStyle.None : DisplayStyle.Flex;
            if (_iconClass != null) _icon.RemoveFromClassList(_iconClass);
            _iconClass = "nl-ico-" + (string.IsNullOrEmpty(o.Icon) ? "spark" : o.Icon);
            _icon.AddToClassList(_iconClass);
            _el.RemoveFromClassList("is-hidden");
            _el.RemoveFromClassList("is-in");
            SlideIn(_el, null);
        }

        // ---------------- keep-out fallback ----------------

        private void Guard()
        {
            if (_shown == null || _el.ClassListContains("is-hidden"))
            {
                if (_fallback) SetFallback(false);
                return;
            }
            if (!_fallback)
            {
                _homeRect = _el.worldBound;
                if (NLScreenGuard.Touches(_homeRect)) SetFallback(true);
            }
            else if (!NLScreenGuard.Touches(_homeRect)) SetFallback(false); // clear again: go home
        }

        private void SetFallback(bool on)
        {
            _fallback = on;
            if (!on)
            {
                _el.style.position = StyleKeyword.Null; _el.style.left = StyleKeyword.Null; _el.style.top = StyleKeyword.Null;
                _el.RemoveFromClassList("is-fallback");
                return;
            }
            // top centre, under the top row (wallet row 44 px + gaps), in the column's own coordinates
            var safe = Root.Q("nl-safe") ?? Root;
            var col = _el.parent;
            var s = safe.worldBound; var c = col.worldBound;
            float w = _homeRect.width;
            _el.style.position = Position.Absolute;
            _el.style.left = s.center.x - w * 0.5f - c.x;
            _el.style.top = s.y + safe.resolvedStyle.paddingTop + 12f + 44f + 8f - c.y;
            _el.AddToClassList("is-fallback");
        }

        // ---------------- +N chooser sheet ----------------

        private void OpenChooser()
        {
            if (_offers.Count < 2 || _chooser == null || NLPhone.IsOpen || NLJobHud.HoldsInput || ChooserOpen) return;
            _chooserFrame = Time.frameCount;
            _chooserKey = null;
            RefreshChooser();
            _chooser.RemoveFromClassList("is-hidden");
            UI.SetRootClass("nl-chooser-open", true);
            _chooser.schedule.Execute(() => _chooser.AddToClassList("is-in")).StartingIn(16);
            NLDevice.Tap();
        }

        private void CloseChooser()
        {
            if (_chooser == null) return;
            _chooserFrame = Time.frameCount;
            _chooser.RemoveFromClassList("is-in");
            UI.SetRootClass("nl-chooser-open", false);
            _chooser.schedule.Execute(() => { if (!_chooser.ClassListContains("is-in")) _chooser.AddToClassList("is-hidden"); })
                    .StartingIn(NLUIRoot.ReducedMotion ? 0 : 230);
        }

        private void RefreshChooser()
        {
            // rebuild only when the offers (ids, verbs, order) change
            var sb = new System.Text.StringBuilder();
            foreach (var o in _offers) sb.Append(o.Id).Append('|').Append(o.Verb).Append('|').Append(o.Subject).Append(';');
            string key = sb.ToString();
            if (key == _chooserKey) return;
            _chooserKey = key;
            _chooserList.Clear();
            for (int i = 0; i < _offers.Count; i++)
            {
                var o = _offers[i];
                var row = new VisualElement();
                row.AddToClassList("nl-chooser-row");
                if (o.Kind == NLPromptKind.Other) row.AddToClassList("is-locked");
                var tile = new VisualElement(); tile.AddToClassList("nl-icon-tile"); tile.pickingMode = PickingMode.Ignore;
                NLPhone.Icon(tile, string.IsNullOrEmpty(o.Icon) ? "spark" : o.Icon);
                row.Add(tile);
                var text = new VisualElement(); text.AddToClassList("nl-chooser-text"); text.pickingMode = PickingMode.Ignore;
                var v = new Label(o.Verb ?? ""); v.AddToClassList("nl-prompt-verb"); v.pickingMode = PickingMode.Ignore; text.Add(v);
                if (!string.IsNullOrEmpty(o.Subject))
                {
                    var sub = new Label(o.Subject); sub.AddToClassList("nl-prompt-subject"); sub.pickingMode = PickingMode.Ignore; text.Add(sub);
                }
                row.Add(text);
                if (i < 9)
                {
                    var k = new Label((i + 1).ToString()); k.AddToClassList("nl-key"); k.AddToClassList("nl-desktop-only");
                    k.AddToClassList("nl-chooser-key"); k.pickingMode = PickingMode.Ignore; row.Add(k);
                }
                var picked = o;
                row.RegisterCallback<ClickEvent>(_ => Choose(picked));
                _chooserList.Add(row);
            }
        }

        private void ChooserKeys()
        {
            if (_chooserFrame == Time.frameCount) return;
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && (kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            {
                CloseChooser();
                return;
            }
            if (kb == null) return;
            for (int i = 0; i < 9 && i < _offers.Count; i++)
            {
                if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) { Choose(_offers[i]); return; }
            }
        }

        private void Choose(NLPromptOffer o)
        {
            CloseChooser();
            int i = _offers.IndexOf(o);
            if (i < 0) return; // withdrawn while the sheet was open
            NLDevice.Tap();
            if (o.Kind == NLPromptKind.Other)
            {
                if (!string.IsNullOrEmpty(o.Explain)) NLToasts.Show("prompt-why-" + o.Id, NLToastKind.System, o.Verb, o.Explain);
                return;
            }
            o.OnAccept?.Invoke();
        }

        public void Accept()
        {
            var o = _shown;
            if (o == null || NLPhone.IsOpen || NLJobHud.HoldsInput || ChooserOpen) return;
            NLDevice.Tap();
            if (o.Kind == NLPromptKind.Other)
            {
                if (!string.IsNullOrEmpty(o.Explain))
                    NLToasts.Show("prompt-why-" + o.Id, NLToastKind.System, o.Verb, o.Explain);
                return;
            }
            o.OnAccept?.Invoke();
        }
    }
}
