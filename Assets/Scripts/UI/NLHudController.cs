// Naija Streets (Unity rebuild) — NLHudController
// Composition root for the HUD + phone. One runtime UIDocument, one
// VisualElement tree. Builds every manager from the spec (docs/specs/
// hud-and-phone.md), wires NLUIKeys events, and keeps the HUD fed from
// INLGameInfo and IWalletSource. The real game supplies those; until then
// the Editor-only fake feeds drive the verify scene (no fake money or
// fake game data in a player build, ever).

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class NLHudController : MonoBehaviour
    {
        [Header("UI Toolkit assets")]
        [SerializeField] private VisualTreeAsset hudTemplate;
        [SerializeField] private VisualTreeAsset phoneTemplate;

        [Header("Data (leave empty for the fake Editor feed)")]
        [SerializeField] private bool useFakeData = true;

        public IWalletSource Wallet { get; set; }
        public INLGameInfo GameInfo { get; set; }

        // Exposed for the verify harness.
        public NLUIRoot UIRoot { get; private set; }
        public NLHudStack HudStack { get; private set; }
        public NLRadar Radar { get; private set; }
        public NLPromptManager Prompts { get; private set; }
        public NLToastManager Toasts { get; private set; }
        public NLScreenGuard ScreenGuard { get; private set; }
        public NLTouchStick TouchStick { get; private set; }
        public NLPhoneController Phone { get; private set; }
        public VisualElement Root { get; private set; }

        private VisualElement _walletAmount;
        private Label _placeText;
        private Label _clockText;
        private VisualElement _hungerFill;
        private VisualElement _energyFill;
        private VisualElement _pawaFill;
        private VisualElement _wantedRow;
        private VisualElement[] _stars;
        private VisualElement _objectiveRow;
        private Label _objectiveText;
        private VisualElement _rideRow;
        private Label _rideText;
        private VisualElement _hintChip;
        private Button _menuButton;
        private NLTouchButton _sprintButton;
        private NLTouchButton _cameraButton;
        private NLTouchButton _phoneButton;
        private NLButtonControl _sprintControl;
        private bool _cameraToggle;
        private bool _debugOn;
        private bool _lastPortrait;
        private Transform _playerTransform;

        private void Awake()
        {
#if UNITY_EDITOR
            if (Wallet == null) Wallet = new FakeWalletSource();
            if (GameInfo == null) GameInfo = new NLFakeGameInfo();
#else
            if (Wallet == null) Wallet = new NullWalletSource();
            if (GameInfo == null) GameInfo = new NullGameInfo();
#endif
        }

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null) { Debug.LogError("[NLHud] No UIDocument on this GameObject."); return; }
            if (hudTemplate == null) { Debug.LogError("[NLHud] hudTemplate (NLHud.uxml) not assigned."); return; }
            if (phoneTemplate == null) { Debug.LogError("[NLHud] phoneTemplate (NLPhone.uxml) not assigned."); return; }

            Root = doc.rootVisualElement;
            hudTemplate.CloneTree(Root);

            UIRoot = GetComponent<NLUIRoot>();
            if (UIRoot == null) UIRoot = gameObject.AddComponent<NLUIRoot>();
            UIRoot.Bind(Root);

            // Player (guard + radar + prompt anchors). Greybox marker in the
            // verify scene; the real player is tagged Player.
            var playerGo = GameObject.FindWithTag("Player") ?? GameObject.Find("Player_Marker");
            _playerTransform = playerGo != null ? playerGo.transform : null;

            // Left column.
            _placeText = Root.Q<Label>("nl-place-text");
            _clockText = Root.Q<Label>("nl-clock-text");
            _hungerFill = Root.Q<VisualElement>("nl-vital-hunger-fill");
            _energyFill = Root.Q<VisualElement>("nl-vital-energy-fill");
            _pawaFill = Root.Q<VisualElement>("nl-vital-pawa-fill");
            var promptPanel = Root.Q<VisualElement>("nl-prompt");
            var chooserSheet = Root.Q<VisualElement>("nl-chooser-sheet");
            Prompts = new NLPromptManager(promptPanel, chooserSheet, PlayerPosition);

            // Right column.
            _walletAmount = Root.Q<Label>("nl-wallet-amount");
            _wantedRow = Root.Q<VisualElement>("nl-row-wanted");
            _stars = new VisualElement[5];
            for (int i = 0; i < 5; i++) _stars[i] = Root.Q<VisualElement>($"nl-star-{i}");
            _objectiveRow = Root.Q<VisualElement>("nl-row-objective");
            _objectiveText = Root.Q<Label>("nl-objective-text");
            _rideRow = Root.Q<VisualElement>("nl-row-ride");
            _rideText = Root.Q<Label>("nl-ride-text");
            _menuButton = Root.Q<Button>("nl-menu-btn");
            var toastLane = Root.Q<VisualElement>("nl-toast-lane");
            Toasts = new NLToastManager(toastLane);

            HudStack = new NLHudStack(Root.Q<VisualElement>("nl-right"), toastLane);
            HudStack.Register(NLHudStack.RowId.Wallet, Root.Q<VisualElement>("nl-row-wallet"));
            HudStack.Register(NLHudStack.RowId.Wanted, _wantedRow);
            HudStack.Register(NLHudStack.RowId.Objective, _objectiveRow);
            HudStack.Register(NLHudStack.RowId.Ride, _rideRow);
            HudStack.Register(NLHudStack.RowId.Spare, Root.Q<VisualElement>("nl-row-spare"));
            HudStack.Hide(NLHudStack.RowId.Spare); // spare row is a reserved slot, empty until used

            // Radar (component on this GameObject; camera assigned in the scene).
            Radar = GetComponent<NLRadar>();
            if (Radar != null)
            {
                Radar.Setup(_playerTransform, () => 0f, () => Vector3.forward,
                    Root.Q<VisualElement>("nl-radar"), Root.Q<VisualElement>("nl-radar-arrow"));
            }

            // Screen guard: prompt fallback + toast hiding + F9 overlay.
            var stickBase = Root.Q<VisualElement>("nl-stick-base");
            var sprintEl = Root.Q<VisualElement>("nl-btn-sprint");
            var cameraEl = Root.Q<VisualElement>("nl-btn-camera");
            var phoneEl = Root.Q<VisualElement>("nl-btn-phone");
            ScreenGuard = new NLScreenGuard(Camera.main, _playerTransform, promptPanel,
                Toasts, Root.Q<VisualElement>("nl-debug-overlay"), stickBase,
                sprintEl, cameraEl, phoneEl);

            // Touch controls: stick writes into <Gamepad>/leftStick, sprint
            // into <Gamepad>/leftStickPress (same actions as keyboard).
            NLVirtualGamepad.Ensure();
            var stickControl = GetComponent<NLStickControl>();
            if (stickControl == null) stickControl = gameObject.AddComponent<NLStickControl>();
            TouchStick = new NLTouchStick(
                Root.Q<VisualElement>("nl-stick-zone"), stickBase,
                Root.Q<VisualElement>("nl-stick-thumb"), stickControl, this);
            _sprintControl = GetComponent<NLButtonControl>();
            if (_sprintControl == null) _sprintControl = gameObject.AddComponent<NLButtonControl>();
            _sprintButton = new NLTouchButton(sprintEl);
            _sprintButton.OnPressed += () => _sprintControl.SetPressed(true);
            _sprintButton.OnReleased += () => _sprintControl.SetPressed(false);
            _cameraButton = new NLTouchButton(cameraEl);
            _cameraButton.OnPressed += () => { _cameraToggle = !_cameraToggle; };
            _phoneButton = new NLTouchButton(phoneEl);
            _phoneButton.OnPressed += () => Phone?.Toggle();

            // Phone.
            Phone = new NLPhoneController(
                Root.Q<VisualElement>("nl-phone-root"), phoneTemplate, Wallet, GameInfo, UIRoot);
            Phone.OnOpened += () => Root.AddToClassList("nl-phone-open");
            Phone.OnClosed += () => Root.RemoveFromClassList("nl-phone-open");
            if (_menuButton != null) _menuButton.clicked += () => Phone?.Toggle();

            // Keys (NLUIKeys polls the Input System and fires events).
            var keys = GetComponent<NLUIKeys>();
            if (keys == null) keys = gameObject.AddComponent<NLUIKeys>();
            keys.OnPhoneToggle += () => Phone?.Toggle();
            keys.OnMapToggle += () => Phone?.OpenToMap();
            keys.OnCameraCycle += () => { _cameraToggle = !_cameraToggle; };
            keys.OnMenuToggle += () => { if (Phone != null && Phone.IsOpen) Phone.Back(); };
            keys.OnAccept += () => Prompts?.Accept();
            keys.OnChooserToggle += () => Prompts?.ToggleChooser();
            keys.OnDebugOverlayToggle += () => SetDebug(!_debugOn);

            // Wallet feed.
            Wallet.OnChanged += UpdateWallet;
            UpdateWallet();
            UpdateFromGameInfo();

            // Hint chip: desktop only, first three sessions.
            _hintChip = Root.Q<VisualElement>("nl-hint-chip");
            int sessions = PlayerPrefs.GetInt("nl_hint_sessions", 0);
            PlayerPrefs.SetInt("nl_hint_sessions", sessions + 1);
            if (_hintChip != null) _hintChip.style.display = sessions < 3 ? DisplayStyle.Flex : DisplayStyle.None;

            // Demo hook for the verify scene: a fake offer at the player's feet.
            if (useFakeData)
            {
                Prompts.Offer(new PromptOffer
                {
                    Id = "demo-conductor",
                    Kind = PromptKind.Action,
                    Verb = "Take over",
                    Subject = "Danfo conductor",
                    Icon = "briefcase",
                    WorldAnchor = Vector3.zero,
                    OnAccept = () => Toasts.Show("shift-started", ToastKind.Job,
                        "Shift started", "Conductor — Oshodi to Lekki", "briefcase")
                });
            }

            GameInfo.OnChanged += UpdateFromGameInfo;
            _lastPortrait = Screen.height >= Screen.width;
            Toasts.SetPortrait(_lastPortrait);
        }

        private void Update()
        {
            if (Root == null) return;
            Prompts?.Tick();
            Toasts?.Tick();
            ScreenGuard?.Tick();
            Phone?.Tick();
            bool portrait = Screen.height >= Screen.width;
            if (portrait != _lastPortrait)
            {
                _lastPortrait = portrait;
                Toasts?.SetPortrait(portrait);
            }
        }

        public void SetDebug(bool on)
        {
            _debugOn = on;
            ScreenGuard?.SetDebug(on);
        }

        private Vector3 PlayerPosition()
        {
            return _playerTransform != null ? _playerTransform.position : Vector3.zero;
        }

        private void UpdateWallet()
        {
            if (_walletAmount is Label amount)
                amount.text = NLMoney.Short(Wallet.Balance);
        }

        private void UpdateFromGameInfo()
        {
            if (GameInfo == null) return;
            if (_placeText != null) _placeText.text = GameInfo.PlaceName ?? "";
            if (_clockText != null) _clockText.text = GameInfo.ClockText ?? "";
            SetBar(_hungerFill, GameInfo.Hunger01);
            SetBar(_energyFill, GameInfo.Energy01);
            SetBar(_pawaFill, GameInfo.Pawa01);
            SetWanted(GameInfo.WantedLevel);
            SetObjective(GameInfo.ObjectiveText);
            SetRide(GameInfo.RideText);
            HudStack?.Layout();
        }

        private static void SetBar(VisualElement fill, float value)
        {
            if (fill == null) return;
            fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
        }

        private void SetWanted(int level)
        {
            if (HudStack == null) return;
            if (level > 0) HudStack.Show(NLHudStack.RowId.Wanted);
            else HudStack.Hide(NLHudStack.RowId.Wanted);
            for (int i = 0; i < _stars.Length; i++)
                _stars[i]?.EnableInClassList("nl-star--on", i < level);
        }

        private void SetObjective(string text)
        {
            if (HudStack == null) return;
            bool show = !string.IsNullOrEmpty(text);
            if (show) HudStack.Show(NLHudStack.RowId.Objective);
            else HudStack.Hide(NLHudStack.RowId.Objective);
            if (show && _objectiveText != null) _objectiveText.text = text;
        }

        private void SetRide(string text)
        {
            if (HudStack == null) return;
            bool show = !string.IsNullOrEmpty(text);
            if (show) HudStack.Show(NLHudStack.RowId.Ride);
            else HudStack.Hide(NLHudStack.RowId.Ride);
            if (show && _rideText != null) _rideText.text = text;
        }

        // Player-build fallbacks: zero wallet, empty game info. No fake data
        // ever ships in a player build.
        private sealed class NullWalletSource : IWalletSource
        {
            public long Balance => 0;
            public DateTime LastSyncedUtc => DateTime.UtcNow;
            public bool IsLoading => false;
            public bool HasError => true;
            public int HistoryCount => 0;
            public System.Collections.Generic.IReadOnlyList<LedgerEntry> GetHistory(int page, int pageSize)
                => Array.Empty<LedgerEntry>();
            public WeekSummary GetWeekSummary() => new WeekSummary();
            public void Refresh(Action<bool> onDone) => onDone?.Invoke(false);
            public event Action OnChanged;
        }

        private sealed class NullGameInfo : INLGameInfo
        {
            public string PlaceName => "";
            public string ClockText => "";
            public string DayText => "";
            public float Hunger01 => 0f;
            public float Energy01 => 0f;
            public float Pawa01 => 0f;
            public int WantedLevel => 0;
            public string ObjectiveText => null;
            public string RideText => null;
            public string NextTitle => null;
            public string NextSub => null;
            public float PlayerHeadingDegrees => 0f;
            public event Action OnChanged;
        }
    }
}
