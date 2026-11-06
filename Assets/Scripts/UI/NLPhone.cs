// Naija Streets: the in-game phone (HUD-5). Spec docs/specs/hud-and-phone.md 8.1-8.2.
//   - Opens from the phone button, P, the wallet chip, or gamepad Select.
//   - Portrait: full screen, slides up. Landscape/desktop: phone-shaped panel on
//     the right so the street stays visible.
//   - The player stops (PlayerInput.SetLocked) while the phone is up; the world
//     keeps running.
//   - Close: close button, Esc, P again, gamepad East. Back pops the stack.
//   - Only BUILT apps show (no "coming soon" tiles): Bank and Settings for now.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public sealed class NLPhone : NLUIPart
    {
        public enum App { Home, Bank, Settings }

        public static NLPhone Instance { get; private set; }
        public static bool IsOpen { get; private set; }
        /// <summary>Frame the phone last opened or closed, so one Esc press is never used twice.</summary>
        public static int LastChangeFrame { get; private set; } = -1;

        private sealed class PhoneScreen
        {
            public string Title; public VisualElement El; public Action OnShow, OnHide, Tick;
        }

        private readonly List<PhoneScreen> _stack = new List<PhoneScreen>();
        private VisualElement _phone, _scrim, _stackEl, _back;
        private Label _title, _statusClock;
        private NLBankScreen _bank;
        private int _lastMinute = -1;
        private NaijaLife.Player.PlayerInput _playerInput;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; IsOpen = false; }

        protected override void OnBind(VisualElement root)
        {
            _phone = root.Q("nl-phone");
            _scrim = root.Q("nl-phone-scrim");
            _stackEl = root.Q("nl-phone-stack");
            _back = root.Q("nl-phone-back");
            _title = root.Q<Label>("nl-phone-title");
            _statusClock = root.Q<Label>("nl-phone-status-clock");
            root.Q<Button>("nl-phone-close").clicked += Close;
            ((Button)_back).clicked += Back;
            _scrim.RegisterCallback<ClickEvent>(_ => Close());
            // swipe down from the status bar closes on touch
            var status = _phone.Q(className: "nl-phone-status");
            status.pickingMode = PickingMode.Position;
            float downY = 0f; int downId = -1;
            status.RegisterCallback<PointerDownEvent>(e => { downId = e.pointerId; downY = e.position.y; });
            status.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == downId && e.position.y - downY > 60f) Close(); downId = -1; });

            _bank = new NLBankScreen();
            if (IsOpen) { IsOpen = false; Open(App.Home); }
        }

        protected override void Tick()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if (IsOpen && LastChangeFrame != Time.frameCount)
            {
                if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)) Back();
            }
            if (IsOpen)
            {
                var top = _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
                top?.Tick?.Invoke();
                var wat = NLHud.WatNow;
                if (wat.Minute != _lastMinute) { _lastMinute = wat.Minute; _statusClock.text = wat.ToString("HH:mm"); }
            }
            else if (pad != null && pad.selectButton.wasPressedThisFrame) Open(App.Home);
        }

        // ---------------- open / close ----------------

        public void Toggle()
        {
            if (IsOpen) Close(); else Open(App.Home);
        }

        public void Open(App app)
        {
            if (!Bound) return;
            if (!IsOpen)
            {
                IsOpen = true;
                LastChangeFrame = Time.frameCount;
                ClearStack();
                Push(BuildHome());
                SlideIn(_phone);
                SlideIn(_scrim);
                UI.SetRootClass("nl-phone-open", true);
                Lock(true);
                NLDevice.Tap();
            }
            if (app == App.Bank) Push(BuildBank());
            else if (app == App.Settings) Push(BuildSettings());
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            LastChangeFrame = Time.frameCount;
            if (_stack.Count > 0) _stack[_stack.Count - 1].OnHide?.Invoke();
            SlideOut(_phone, 370);
            SlideOut(_scrim);
            UI.SetRootClass("nl-phone-open", false);
            Lock(false);
            _phone.schedule.Execute(() => { if (!IsOpen) ClearStack(); }).StartingIn(380);
        }

        private void Lock(bool on)
        {
            if (_playerInput == null) _playerInput = FindFirstObjectByType<NaijaLife.Player.PlayerInput>();
            if (_playerInput != null) _playerInput.SetLocked(on);
        }

        // ---------------- navigation stack ----------------

        private void Push(PhoneScreen s)
        {
            var prev = _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
            if (prev != null) { prev.El.AddToClassList("is-left"); prev.OnHide?.Invoke(); }
            _stack.Add(s);
            s.El.AddToClassList("nl-screen");
            if (prev != null) s.El.AddToClassList("is-right");
            _stackEl.Add(s.El);
            if (prev != null) s.El.schedule.Execute(() => s.El.RemoveFromClassList("is-right")).StartingIn(16);
            Bar();
            s.OnShow?.Invoke();
        }

        public void Back()
        {
            if (_stack.Count <= 1) { Close(); return; }
            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            top.OnHide?.Invoke();
            top.El.AddToClassList("is-right");
            top.El.schedule.Execute(() => top.El.RemoveFromHierarchy()).StartingIn(NLUIRoot.ReducedMotion ? 0 : 230);
            var prev = _stack[_stack.Count - 1];
            prev.El.RemoveFromClassList("is-left");
            Bar();
            prev.OnShow?.Invoke();
        }

        private void ClearStack()
        {
            foreach (var s in _stack) s.El.RemoveFromHierarchy();
            _stack.Clear();
            _stackEl?.Clear();
        }

        private void Bar()
        {
            var top = _stack[_stack.Count - 1];
            _title.text = top.Title ?? "";
            _back.EnableInClassList("is-hidden", _stack.Count <= 1);
            _back.pickingMode = _stack.Count <= 1 ? PickingMode.Ignore : PickingMode.Position;
        }

        // ---------------- screens ----------------

        private PhoneScreen BuildHome()
        {
            var el = new VisualElement();

            var head = Add(el, "nl-home-head");
            Add(head, "nl-home-logo");
            var cw = Add(head, "nl-home-clockwrap");
            var day = new Label(); day.AddToClassList("nl-home-day"); cw.Add(day);
            var clock = new Label(); clock.AddToClassList("nl-home-clock"); cw.Add(clock);

            var next = Add(el, "nl-next");
            var tile = Add(next, "nl-icon-tile"); Icon(tile, "route");
            var nt = Add(next, "nl-next-text");
            var nTitle = new Label(); nTitle.AddToClassList("nl-next-title"); nt.Add(nTitle);
            var nLine = new Label(); nLine.AddToClassList("nl-next-line"); nt.Add(nLine);

            var apps = Add(el, "nl-apps");
            AppTile(apps, "Bank", "wallet", "nl-app--bank", () => Push(BuildBank()));
            AppTile(apps, "Settings", "gear", null, () => Push(BuildSettings()));

            int last = -1;
            void Refresh()
            {
                var wat = NLHud.WatNow;
                if (wat.Minute == last) return;
                last = wat.Minute;
                day.text = wat.ToString("dddd").ToUpperInvariant() + "  WAT";
                clock.text = wat.ToString("HH:mm");
                var goal = NLPhoneNext.Current;
                next.EnableInClassList("is-hidden", goal == null);
                if (goal != null) { nTitle.text = goal.Value.title; nLine.text = goal.Value.line; }
            }
            return new PhoneScreen { Title = "", El = el, OnShow = () => { last = -1; Refresh(); }, Tick = Refresh };
        }

        private PhoneScreen BuildBank()
        {
            var el = _bank.Build();
            return new PhoneScreen { Title = "Bank", El = el, OnShow = _bank.OnShow, OnHide = _bank.OnHide, Tick = _bank.Tick };
        }

        private PhoneScreen BuildSettings()
        {
            var el = new VisualElement();
            el.AddToClassList("nl-bank");
            SwitchRow(el, "Reduce motion", "Less sliding and counting", () => NLUIRoot.ReducedMotion, v => NLUIRoot.ReducedMotion = v);
            SwitchRow(el, "Tap to sprint", "One tap on, one tap off", () => NLTouchControls.SprintToggle, v => NLTouchControls.SprintToggle = v);
            SwitchRow(el, "Key hints", "Show P and V on screen", () => PlayerPrefs.GetInt("nl.ui.sessions", 1) <= 3,
                v =>
                {
                    PlayerPrefs.SetInt("nl.ui.sessions", v ? 0 : 99);
                    Root?.Q("nl-keyhints")?.EnableInClassList("is-hidden", !v);
                });
            return new PhoneScreen { Title = "Settings", El = el };
        }

        // ---------------- small builders ----------------

        internal static VisualElement Add(VisualElement parent, string cls)
        {
            var e = new VisualElement();
            if (cls != null) e.AddToClassList(cls);
            parent.Add(e);
            return e;
        }

        internal static VisualElement Icon(VisualElement parent, string name)
        {
            var e = Add(parent, "nl-icon");
            e.AddToClassList("nl-ico-" + name);
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        private static void AppTile(VisualElement grid, string name, string icon, string extra, Action open)
        {
            var app = Add(grid, "nl-app");
            if (extra != null) app.AddToClassList(extra);
            var t = Add(app, "nl-app-tile"); Icon(t, icon);
            var l = new Label(name); l.AddToClassList("nl-app-name"); app.Add(l);
            app.RegisterCallback<ClickEvent>(_ => { NLDevice.Tap(); open(); });
        }

        private static void SwitchRow(VisualElement parent, string title, string line, Func<bool> get, Action<bool> set)
        {
            var row = Add(parent, "nl-week-row");
            row.style.height = 64;
            var text = Add(row, null);
            var t = new Label(title); t.AddToClassList("nl-txn-title"); text.Add(t);
            var s = new Label(line); s.AddToClassList("nl-txn-line"); text.Add(s);
            var sw = Add(row, "nl-switch");
            Add(sw, "nl-switch-knob");
            void Paint() => sw.EnableInClassList("is-on", get());
            Paint();
            row.RegisterCallback<ClickEvent>(_ => { set(!get()); Paint(); NLDevice.Tap(); });
        }
    }

    /// <summary>What the phone home "Next" card shows. Story/goal systems set it.</summary>
    public static class NLPhoneNext
    {
        public static (string title, string line)? Current { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Current = null;
    }
}
