// Naija Streets: job pieces of the HUD kit (HUD-8). Spec docs/specs/jobs-m4.md
// section 6.1 and hud-and-phone.md section 5.1. The jobs code reaches these
// through NLJobUiBridge (IJobUi); nothing here knows about shifts or pay rules.
//   - Shift chip: a row under the wallet (time left, trips, earned this shift).
//     "Earned" is pending server pay, so it is dim and never touches the wallet.
//   - Busy row: "Saving your pay..." under the shift chip, with a spinner.
//   - Shout button: replaces the on-foot buttons bottom-right. A ring shrinks
//     onto the target band; the band lights green when a tap would count.
//     Seat strip (14 dots) and seconds left sit above it. Space / gamepad South.
//   - Change sheet: bottom, at most 35% of the screen height, three amounts.
//     Keys 1 2 3, gamepad West / North / East.
//   - End-of-shift card: same bottom sheet, one Okay button (E / Enter / South).
// While a sheet or the card is up, NLPrompts holds its keys (HoldsInput);
// during the shout only gamepad South is taken (HoldsSouth), so E still works.
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public sealed class NLJobHud : NLUIPart
    {
        public static NLJobHud Instance { get; private set; }

        /// <summary>True while the change sheet or the shift card is up: prompts wait.</summary>
        public static bool HoldsInput =>
            Instance != null && Instance.Bound && (Instance._onPick != null || Instance._onClose != null || Instance._usedFrame == Time.frameCount);

        /// <summary>True while gamepad South belongs to the job HUD (shout, sheet or card). E still reaches the prompt during the shout.</summary>
        public static bool HoldsSouth => HoldsInput || (Instance != null && Instance.Bound && Instance._onShout != null);

        private const float TargetPx = 64f;   // ring diameter when RingScale == 1 (the band)
        private const int MaxSeatDots = 20;

        // shift chip + busy row
        private VisualElement _chip, _busy, _busyIcon;
        private Label _chipTime, _chipTrips, _chipPay, _chipNote, _busyText;
        // shout
        private VisualElement _shout, _ring, _band, _shoutBtn, _seats;
        private Label _shoutTime, _shoutSay;
        private Action _onShout;
        private int _seatsMax = -1;
        // sheet (change + summary)
        private VisualElement _sheetWrap, _sheet, _timer, _options, _okBtn;
        private Label _sheetTitle, _sheetLine, _sheetResult;
        private readonly Button[] _opt = new Button[3];
        private Action<int> _onPick;
        private Action _onClose;
        private float _answerFull = 6f;
        private int _lastOptionsKey;
        private float _spin;
        private int _usedFrame = -1; // a key used here this frame is never used again by NLPrompts

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------------- public API (called by NLJobUiBridge) ----------------

        public void ShiftChip(bool show, float secondsLeft, int trips, long pending, string note)
        {
            if (!Bound) return;
            _chip.EnableInClassList("is-hidden", !show);
            if (!show) return;
            int s = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
            _chipTime.text = (s / 60) + ":" + (s % 60).ToString("00");
            _chipTrips.text = trips == 1 ? "1 trip" : trips + " trips";
            _chipPay.text = pending > 0 ? "+" + NLMoney.Short(pending) : NLMoney.Short(0);
            _chipNote.text = note ?? "";
            _chipNote.EnableInClassList("is-hidden", string.IsNullOrEmpty(note));
            _chip.EnableInClassList("is-low", s <= 60);
        }

        public void Busy(string text)
        {
            if (!Bound) return;
            _busy.EnableInClassList("is-hidden", string.IsNullOrEmpty(text));
            _busyText.text = text ?? "";
        }

        /// <summary>Show or update the shout button. onShout null hides it.</summary>
        public void Shout(float ringScale, bool onBand, int seats, int seatsMax, float secondsLeft, string say, Action onShout)
        {
            if (!Bound) return;
            bool show = onShout != null || ringScale > 0f;
            _onShout = show ? onShout : null;
            UI.SetRootClass("nl-job-shout", show);
            if (!show) { _shout.EnableInClassList("is-hidden", true); return; }
            _shout.EnableInClassList("is-hidden", false);

            float d = TargetPx * Mathf.Clamp(ringScale, 0.2f, 2f);
            _ring.style.width = d; _ring.style.height = d;
            var r = new StyleLength(d * 0.5f);
            _ring.style.borderTopLeftRadius = r; _ring.style.borderTopRightRadius = r;
            _ring.style.borderBottomLeftRadius = r; _ring.style.borderBottomRightRadius = r;
            _ring.EnableInClassList("is-band", onBand);
            _band.EnableInClassList("is-band", onBand);

            seatsMax = Mathf.Clamp(seatsMax, 0, MaxSeatDots);
            if (seatsMax != _seatsMax)
            {
                _seatsMax = seatsMax;
                _seats.Clear();
                for (int i = 0; i < seatsMax; i++) NLPhone.Add(_seats, "nl-seat").pickingMode = PickingMode.Ignore;
            }
            for (int i = 0; i < _seats.childCount; i++) _seats[i].EnableInClassList("is-full", i < seats);
            _shoutTime.text = Mathf.Max(0, Mathf.CeilToInt(secondsLeft)) + "s";
            _shoutSay.text = say ?? "";
            _shoutSay.EnableInClassList("is-hidden", string.IsNullOrEmpty(say));
        }

        public void HideShout() => Shout(0f, false, 0, _seatsMax < 0 ? 0 : _seatsMax, 0f, null, null);

        /// <summary>
        /// Change sheet. options are change amounts in naira. result non-null shows
        /// the answer line and locks the buttons. Pass hide=true to close it.
        /// </summary>
        public void Change(bool hide, int index, int of, int note, int fare, int[] options, float secondsLeft, string result, Action<int> onPick)
        {
            if (!Bound) return;
            if (hide) { if (_onClose == null) CloseSheet(); _onPick = null; return; }
            _onClose = null;
            _onPick = result == null ? onPick : null;
            _okBtn.EnableInClassList("is-hidden", true);
            _options.EnableInClassList("is-hidden", false);
            _timer.parent.EnableInClassList("is-hidden", false);
            _sheetTitle.text = "Collect fares  " + (index + 1) + " of " + of;
            _sheetLine.text = NLMoney.Full(note) + " note for a " + NLMoney.Full(fare) + " fare. Give change.";

            int key = (options == null ? 0 : options.Length) * 7919 + index * 31 + note;
            if (options != null) foreach (var o in options) key = key * 17 + o;
            if (key != _lastOptionsKey)
            {
                _lastOptionsKey = key;
                _answerFull = Mathf.Max(1f, secondsLeft); // first frame of this fare sets the bar's full length
            }
            for (int i = 0; i < _opt.Length; i++)
            {
                bool has = options != null && i < options.Length;
                _opt[i].EnableInClassList("is-hidden", !has);
                if (has) _opt[i].text = NLMoney.Full(options[i]);
                _opt[i].SetEnabled(result == null);
            }
            _timer.style.width = Length.Percent(Mathf.Clamp01(secondsLeft / _answerFull) * 100f);
            _timer.EnableInClassList("is-low", secondsLeft <= 2f);
            _sheetResult.text = result ?? "";
            _sheetResult.EnableInClassList("is-hidden", result == null);
            _sheetResult.EnableInClassList("is-good", result != null && result.StartsWith("Correct"));
            OpenSheet();
        }

        /// <summary>End-of-shift card. body lines split on '\n'. onClose may be null.</summary>
        public void Summary(string title, string body, Action onClose)
        {
            if (!Bound) return;
            _onPick = null;
            _onClose = onClose ?? (() => { });
            _options.EnableInClassList("is-hidden", true);
            _timer.parent.EnableInClassList("is-hidden", true);
            _okBtn.EnableInClassList("is-hidden", false);
            _sheetTitle.text = title ?? "";
            _sheetLine.text = body ?? "";
            _sheetResult.EnableInClassList("is-hidden", true);
            _lastOptionsKey = 0;
            OpenSheet();
        }

        /// <summary>Hide every job piece (shift ended, scene change, test menu).</summary>
        public void ClearAll()
        {
            if (!Bound) return;
            HideShout();
            ShiftChip(false, 0f, 0, 0, null);
            Busy(null);
            CloseSheet();
        }

        // ---------------- build ----------------

        protected override void OnBind(VisualElement root)
        {
            var rows = root.Q("nl-rows");
            var safe = root.Q("nl-safe");

            // shift chip
            _chip = NLPhone.Add(rows, "nl-chip");
            _chip.AddToClassList("nl-shift");
            _chip.AddToClassList("is-hidden");
            _chip.pickingMode = PickingMode.Ignore;
            var top = NLPhone.Add(_chip, "nl-shift-top");
            NLPhone.Icon(top, "clock").AddToClassList("nl-icon--small");
            _chipTime = L(top, "nl-shift-time");
            _chipTrips = L(top, "nl-shift-trips");
            _chipPay = L(top, "nl-shift-pay");
            _chipNote = L(_chip, "nl-shift-note");

            // busy row
            _busy = NLPhone.Add(rows, "nl-chip");
            _busy.AddToClassList("nl-busy");
            _busy.AddToClassList("is-hidden");
            _busy.pickingMode = PickingMode.Ignore;
            _busyIcon = NLPhone.Icon(_busy, "refresh");
            _busyIcon.AddToClassList("nl-icon--small");
            _busyText = L(_busy, "nl-busy-text");

            // shout button (bottom-right, where the on-foot buttons live)
            _shout = NLPhone.Add(safe, "nl-shout");
            _shout.AddToClassList("is-hidden");
            _shout.pickingMode = PickingMode.Ignore;
            var info = NLPhone.Add(_shout, "nl-shout-info");
            info.pickingMode = PickingMode.Ignore;
            _seats = NLPhone.Add(info, "nl-seats");
            _seats.pickingMode = PickingMode.Ignore;
            _shoutTime = L(info, "nl-shout-time");
            _shoutSay = L(_shout, "nl-shout-say");
            var pad = NLPhone.Add(_shout, "nl-shout-pad");
            pad.pickingMode = PickingMode.Ignore;
            _band = NLPhone.Add(pad, "nl-shout-band");
            _band.pickingMode = PickingMode.Ignore;
            _ring = NLPhone.Add(pad, "nl-shout-ring");
            _ring.pickingMode = PickingMode.Ignore;
            _shoutBtn = NLPhone.Add(pad, "nl-shout-btn");
            NLPhone.Icon(_shoutBtn, "megaphone").AddToClassList("nl-icon--ink");
            var key = new Label("Space"); key.AddToClassList("nl-key"); key.AddToClassList("nl-desktop-only"); key.AddToClassList("nl-shout-key");
            key.pickingMode = PickingMode.Ignore; pad.Add(key);
            _shoutBtn.RegisterCallback<PointerDownEvent>(e => { e.StopPropagation(); DoShout(); });
            _seatsMax = -1;

            // bottom sheet: change and the end-of-shift card
            _sheetWrap = NLPhone.Add(safe, "nl-job-sheet-wrap");
            _sheetWrap.pickingMode = PickingMode.Ignore;
            _sheet = NLPhone.Add(_sheetWrap, "nl-job-sheet");
            _sheet.AddToClassList("is-hidden");
            _sheetTitle = L(_sheet, "nl-job-sheet-title");
            var track = NLPhone.Add(_sheet, "nl-job-timer");
            _timer = NLPhone.Add(track, "nl-job-timer-fill");
            _sheetLine = L(_sheet, "nl-job-sheet-line");
            _options = NLPhone.Add(_sheet, "nl-job-options");
            for (int i = 0; i < _opt.Length; i++)
            {
                int n = i;
                var b = new Button(() => Pick(n)) { text = "" };
                b.AddToClassList("nl-job-option");
                if (i == _opt.Length - 1) b.AddToClassList("nl-job-option--last");
                _options.Add(b);
                _opt[i] = b;
            }
            _sheetResult = L(_sheet, "nl-job-sheet-result");
            var ok = new Button(CloseSummary) { text = "Okay" };
            ok.AddToClassList("nl-job-ok");
            _sheet.Add(ok);
            _okBtn = ok;

            _onShout = null; _onPick = null; _onClose = null;
            UI.SetRootClass("nl-job-shout", false);
            UI.SetRootClass("nl-job-sheet-open", false);
        }

        protected override void OnUnbind()
        {
            _chip?.RemoveFromHierarchy(); _busy?.RemoveFromHierarchy();
            _shout?.RemoveFromHierarchy(); _sheetWrap?.RemoveFromHierarchy();
            UI?.SetRootClass("nl-job-shout", false);
            UI?.SetRootClass("nl-job-sheet-open", false);
        }

        private static Label L(VisualElement parent, string cls)
        {
            var l = new Label();
            l.AddToClassList(cls);
            l.pickingMode = PickingMode.Ignore;
            parent.Add(l);
            return l;
        }

        private void OpenSheet()
        {
            if (_sheet.ClassListContains("is-in")) return;
            _sheet.RemoveFromClassList("is-hidden");
            UI.SetRootClass("nl-job-sheet-open", true);
            _sheet.schedule.Execute(() => _sheet.AddToClassList("is-in")).StartingIn(16);
        }

        private void CloseSheet()
        {
            _onPick = null; _onClose = null;
            UI.SetRootClass("nl-job-sheet-open", false);
            _sheet.RemoveFromClassList("is-in");
            _sheet.schedule.Execute(() => { if (!_sheet.ClassListContains("is-in")) _sheet.AddToClassList("is-hidden"); })
                  .StartingIn(NLUIRoot.ReducedMotion ? 0 : 230);
        }

        // ---------------- input ----------------

        private void DoShout()
        {
            if (_onShout == null) return;
            _shoutBtn.AddToClassList("is-down");
            _shoutBtn.schedule.Execute(() => _shoutBtn.RemoveFromClassList("is-down")).StartingIn(90);
            _usedFrame = Time.frameCount;
            NLDevice.Tap();
            _onShout();
        }

        private void Pick(int i)
        {
            var cb = _onPick;
            if (cb == null) return;
            _onPick = null; // one answer per fare; the jobs code shows the result next
            _usedFrame = Time.frameCount;
            NLDevice.Tap();
            cb(i);
        }

        private void CloseSummary()
        {
            var cb = _onClose;
            if (cb == null) return;
            _usedFrame = Time.frameCount;
            CloseSheet();
            NLDevice.Tap();
            cb();
        }

        protected override void Tick()
        {
            if (!_busy.ClassListContains("is-hidden"))
            {
                _spin = (_spin + Time.unscaledDeltaTime * 360f) % 360f;
                _busyIcon.style.rotate = new Rotate(new Angle(_spin, AngleUnit.Degree));
            }

            if (NLPhone.IsOpen) return;
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (_onClose != null)
            {
                if ((kb != null && (kb.eKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                    || (gp != null && gp.buttonSouth.wasPressedThisFrame)) CloseSummary();
                return;
            }
            if (_onPick != null)
            {
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Pick(0);
                    else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Pick(1);
                    else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Pick(2);
                }
                if (_onPick != null && gp != null)
                {
                    if (gp.buttonWest.wasPressedThisFrame) Pick(0);
                    else if (gp.buttonNorth.wasPressedThisFrame) Pick(1);
                    else if (gp.buttonEast.wasPressedThisFrame) Pick(2);
                }
            }
            if (_onShout != null)
            {
                if ((kb != null && kb.spaceKey.wasPressedThisFrame) || (gp != null && gp.buttonSouth.wasPressedThisFrame)) DoShout();
            }
        }
    }
}
