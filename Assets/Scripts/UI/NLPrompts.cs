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
            _more.RegisterCallback<ClickEvent>(e => { NextOffer(); e.StopPropagation(); });
            _shown = null;
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

            if (NLPhone.IsOpen) return;
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) Accept();
            if (kb != null && kb.tabKey.wasPressedThisFrame) NextOffer();
            var pad = Gamepad.current;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) Accept();
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

        public void Accept()
        {
            var o = _shown;
            if (o == null || NLPhone.IsOpen) return;
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
