// Naija Streets: on-foot HUD (HUD-3). Spec docs/specs/hud-and-phone.md 5.1.
//   - Wallet chip (NLMoney.Short), counts up/down over 0.6 s, taps open Bank.
//   - Place + WAT clock pill on the radar's bottom edge.
//   - Vitals strip: hunger, energy, Pawa (0..1). Call NLHud.SetVitals.
//   - Desktop keys: P phone, Esc menu. Key hint chip for the first 3 sessions.
//   - Wallet polling every 5 minutes while the game runs.
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public sealed class NLHud : NLUIPart
    {
        public static NLHud Instance { get; private set; }

        private const string SessionsKey = "nl.ui.sessions";
        private const int HintSessions = 3;
        private const float CountSeconds = 0.6f;

        [Tooltip("District shown in the place pill until the world tells us (M2 sets it per district).")]
        [SerializeField] private string startPlace = "Oshodi";

        private Label _wallet, _placeName, _placeClock;
        private VisualElement _walletChip, _hunger, _energy, _pawa, _hints, _menu;
        private float _shownMoney, _countFrom, _countTo, _countT = 1f;
        private float _nextPoll;
        private int _lastMinute = -1;
        private static Vector3 _vitals = new Vector3(0.7f, 0.6f, 0.8f);
        private static string _place;

        /// <summary>Game time is WAT (UTC+1) for everyone; never the device clock.</summary>
        public static DateTime WatNow => DateTime.UtcNow.AddHours(1);

        /// <summary>Hunger, energy and Pawa, each 0..1. Below 0.25 the bar turns red.</summary>
        public static void SetVitals(float hunger, float energy, float pawa)
        {
            _vitals = new Vector3(Mathf.Clamp01(hunger), Mathf.Clamp01(energy), Mathf.Clamp01(pawa));
            Instance?.ApplyVitals();
        }

        /// <summary>District name for the place pill, for example "Oshodi" or "Wuse".</summary>
        public static void SetPlace(string district)
        {
            _place = district;
            if (Instance != null && Instance._placeName != null)
                Instance._placeName.text = (district ?? "").ToUpperInvariant();
        }

        private void Awake()
        {
            Instance = this;
            PlayerPrefs.SetInt(SessionsKey, PlayerPrefs.GetInt(SessionsKey, 0) + 1);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        protected override void OnBind(VisualElement root)
        {
            _walletChip = root.Q("nl-wallet");
            _wallet = root.Q<Label>("nl-wallet-value");
            _placeName = root.Q<Label>("nl-place-name");
            _placeClock = root.Q<Label>("nl-place-clock");
            _hunger = root.Q("nl-vital-hunger");
            _energy = root.Q("nl-vital-energy");
            _pawa = root.Q("nl-vital-pawa");
            _hints = root.Q("nl-keyhints");
            _menu = root.Q("nl-menu-btn");

            _placeName.text = (_place ?? startPlace).ToUpperInvariant();
            _hints.EnableInClassList("is-hidden", PlayerPrefs.GetInt(SessionsKey, 1) > HintSessions);
            _walletChip.RegisterCallback<ClickEvent>(_ => { if (!NLPhone.IsOpen) NLPhone.Instance?.Open(NLPhone.App.Bank); });
            _menu.RegisterCallback<ClickEvent>(_ => { if (!NLPhone.IsOpen) NLPhone.Instance?.Open(NLPhone.App.Settings); });
            root.Q("nl-vitals").RegisterCallback<ClickEvent>(_ =>
                NLToasts.Show("needs", NLToastKind.System, "Hunger, energy, Pawa", "Chop and sleep keep them full"));

            NLWallet.BalanceChanged += OnBalance;
            NLWallet.SourceChanged += OnSource;
            ShowWalletNow();
            ApplyVitals();
            _lastMinute = -1;
            _nextPoll = Time.unscaledTime + NLWallet.PollSeconds;
        }

        protected override void OnUnbind()
        {
            NLWallet.BalanceChanged -= OnBalance;
            NLWallet.SourceChanged -= OnSource;
        }

        protected override void Tick()
        {
            // clock pill, once a minute
            var wat = WatNow;
            if (wat.Minute != _lastMinute)
            {
                _lastMinute = wat.Minute;
                _placeClock.text = wat.ToString("HH:mm");
            }

            // wallet count-up
            if (_countT < 1f)
            {
                _countT = NLUIRoot.ReducedMotion ? 1f : Mathf.Min(1f, _countT + Time.unscaledDeltaTime / CountSeconds);
                float e = 1f - Mathf.Pow(1f - _countT, 3f);
                _shownMoney = Mathf.Lerp(_countFrom, _countTo, e);
                _wallet.text = NLMoney.Short((long)Math.Round(_shownMoney));
                if (_countT >= 1f) _wallet.RemoveFromClassList("is-up");
            }

            // 5 minute poll
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + NLWallet.PollSeconds;
                NLWallet.Refresh();
            }

            // desktop keys
            var kb = Keyboard.current;
            if (kb != null && NLPhone.LastChangeFrame != Time.frameCount)
            {
                if (kb.pKey.wasPressedThisFrame) NLPhone.Instance?.Toggle();
                else if (kb.escapeKey.wasPressedThisFrame && !NLPhone.IsOpen && !NLPrompts.ChooserOpen && NLPrompts.ChooserFrame != Time.frameCount)
                    NLPhone.Instance?.Open(NLPhone.App.Settings);
            }
        }

        private void OnSource() => ShowWalletNow();

        private void ShowWalletNow()
        {
            if (_wallet == null) return;
            if (!NLWallet.HasBalance)
            {
                _wallet.text = NLMoney.Sign + "...";
                _wallet.AddToClassList("is-loading");
                return;
            }
            _wallet.RemoveFromClassList("is-loading");
            _shownMoney = _countTo = NLWallet.Balance;
            _countT = 1f;
            _wallet.text = NLMoney.Short(NLWallet.Balance);
        }

        private void OnBalance(long oldValue, long newValue, string reason)
        {
            if (_wallet == null) return;
            _wallet.RemoveFromClassList("is-loading");
            _countFrom = _countT < 1f ? _shownMoney : oldValue;
            _countTo = newValue;
            _countT = 0f;
            _wallet.EnableInClassList("is-up", newValue > oldValue);
            long delta = newValue - oldValue;
            if (delta > 0 && reason != null)
                NLToasts.Show("money", NLToastKind.Money, NLMoney.Delta(delta), reason, addMoney: delta);
        }

        private void ApplyVitals()
        {
            if (_hunger == null) return;
            Set(_hunger, _vitals.x); Set(_energy, _vitals.y); Set(_pawa, _vitals.z);
        }

        private static void Set(VisualElement fill, float v)
        {
            fill.style.width = Length.Percent(v * 100f);
            fill.EnableInClassList("is-low", v < 0.25f);
        }
    }
}
