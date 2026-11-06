// Naija Streets (Unity rebuild) — NLPhoneController
// The in-game phone. Spec section 8. Opens with P (or the touch button),
// closes with Esc / P / the close button / the scrim (portrait only).
// While open the game waits (timeScale 0) and the HUD hides. Home is a
// grid of apps; Bank, Map and Settings are real; the rest arrive with
// their features. Slide up 0.36 s (portrait) / panel on the right third.

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLPhoneController
    {
        private readonly VisualElement _phoneRoot;
        private readonly VisualElement _phone;
        private readonly VisualElement _panel;
        private readonly VisualElement _scrim;
        private readonly VisualElement _navBar;
        private readonly Label _navTitle;
        private readonly Button _backButton;
        private readonly Button _closeButton;
        private readonly VisualElement _screenContainer;
        private readonly VisualElement _homeElement;
        private readonly VisualElement _grid;
        private readonly Label _homeClock;
        private readonly Label _homeDay;
        private readonly Label _homeBigClock;
        private readonly Label _nextTitle;
        private readonly Label _nextSub;
        private readonly Label _statusClock;

        private readonly NLPhoneNav _nav;
        private readonly NLBankScreen _bank;
        private readonly NLSettingsScreen _settings;
        private readonly VisualElement _mapScreen;
        private readonly IWalletSource _wallet;
        private readonly INLGameInfo _info;
        private readonly NLUIRoot _uiRoot;

        private float _savedTimeScale = 1f;
        private bool _isOpen;
        private UnityEngine.UIElements.IVisualElementScheduledItem _pendingHide;

        public bool IsOpen => _isOpen;
        public event Action OnOpened;
        public event Action OnClosed;

        public NLPhoneController(VisualElement phoneRoot, VisualTreeAsset phoneTemplate,
            IWalletSource wallet, INLGameInfo info, NLUIRoot uiRoot)
        {
            _phoneRoot = phoneRoot;
            _wallet = wallet;
            _info = info;
            _uiRoot = uiRoot;

            var clone = phoneTemplate.CloneTree();
            _phoneRoot.Add(clone);
            _phone = _phoneRoot.Q<VisualElement>("nl-phone");
            _panel = _phoneRoot.Q<VisualElement>("nl-phone-panel");
            _scrim = _phoneRoot.Q<VisualElement>("nl-phone-scrim");
            _navBar = _phoneRoot.Q<VisualElement>("nl-phone-nav");
            _navTitle = _phoneRoot.Q<Label>("nl-phone-title");
            _backButton = _phoneRoot.Q<Button>("nl-phone-back");
            _closeButton = _phoneRoot.Q<Button>("nl-phone-close");
            _screenContainer = _phoneRoot.Q<VisualElement>("nl-phone-screen");
            _homeElement = _phoneRoot.Q<VisualElement>("nl-phone-home");
            _grid = _phoneRoot.Q<VisualElement>("nl-phone-grid");
            _homeClock = _phoneRoot.Q<Label>("nl-phone-clock");
            _homeDay = _phoneRoot.Q<Label>("nl-phone-day");
            _homeBigClock = _phoneRoot.Q<Label>("nl-phone-big-clock");
            _nextTitle = _phoneRoot.Q<Label>("nl-phone-next-title");
            _nextSub = _phoneRoot.Q<Label>("nl-phone-next-sub");
            _statusClock = _phoneRoot.Q<Label>("nl-phone-clock");

            _nav = new NLPhoneNav(_screenContainer, _navBar, _navTitle, _backButton, _closeButton, _homeElement);
            _nav.OnCloseRequested += Close;

            _bank = new NLBankScreen(wallet, BuildBankScreen());
            _settings = new NLSettingsScreen(BuildSettingsScreen());
            _mapScreen = BuildMapScreen();
            BuildHomeGrid();
            BuildSettingsModel();

            _scrim.RegisterCallback<ClickEvent>(_ => Close());
            _closeButton.clicked += Close;
            UpdateClock();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _pendingHide?.Pause(); // a close 0.36 s ago must not hide us now
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _phoneRoot.style.display = DisplayStyle.Flex;
            _nav.ResetToHome();
            UpdateClock();
            _phone.schedule.Execute(() => _phone.AddToClassList("nl-phone--open"));
            OnOpened?.Invoke();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _phone.RemoveFromClassList("nl-phone--open");
            // Hide after the 0.36 s close animation.
            _pendingHide = _phone.schedule.Execute(() => _phoneRoot.style.display = DisplayStyle.None).StartingIn(380);
            Time.timeScale = _savedTimeScale;
            OnClosed?.Invoke();
        }

        public void Toggle() { if (_isOpen) Close(); else Open(); }

        public void OpenToMap()
        {
            Open();
            _nav.Push(new NLPhoneNav.Screen { Title = "Map", Element = _mapScreen });
        }

        public bool Back()
        {
            if (!_isOpen) return false;
            if (!_nav.IsHome) { _nav.Pop(); return true; }
            Close();
            return true;
        }

        public void Tick()
        {
            if (!_isOpen) return;
            _bank.Tick();
        }

        private void UpdateClock()
        {
            string clock = _info?.ClockText ?? "07:42";
            if (_statusClock != null) _statusClock.text = clock;
            if (_homeClock != null) _homeClock.text = clock;
            if (_homeBigClock != null) _homeBigClock.text = clock;
            if (_homeDay != null) _homeDay.text = _info?.DayText ?? "TUESDAY, WAT";
            if (_nextTitle != null) _nextTitle.text = _info?.NextTitle ?? NLStrings.NoObjective;
            if (_nextSub != null) _nextSub.text = _info?.NextSub ?? "";
        }

        private void BuildHomeGrid()
        {
            _grid.Clear();
            AddApp("Bank", "wallet", () => _nav.Push(new NLPhoneNav.Screen
            {
                Title = "Bank",
                Element = _bank.Root,
                OnShow = () => _bank.OnShow()
            }));
            AddApp("Map", "map", () => _nav.Push(new NLPhoneNav.Screen
            {
                Title = "Map",
                Element = _mapScreen
            }));
            AddApp("Settings", "settings", () => _nav.Push(new NLPhoneNav.Screen
            {
                Title = "Settings",
                Element = _settings.Root
            }));
        }

        private void AddApp(string label, string icon, Action open)
        {
            var app = new VisualElement();
            app.AddToClassList("nl-phone-app");
            var tile = new Button();
            tile.AddToClassList("nl-phone-app-tile");
            var iconEl = new VisualElement();
            iconEl.AddToClassList("nl-icon");
            NLIcon.Set(iconEl, icon);
            tile.Add(iconEl);
            tile.clicked += open;
            var name = new Label(label);
            name.AddToClassList("nl-phone-app-label");
            app.Add(tile);
            app.Add(name);
            _grid.Add(app);
        }

        private VisualElement BuildBankScreen()
        {
            var root = new VisualElement();
            root.AddToClassList("nl-phone-page");
            root.style.display = DisplayStyle.None;

            var card = new VisualElement();
            card.AddToClassList("nl-bank-card");
            var kicker = new Label("BALANCE");
            kicker.AddToClassList("nl-text-kicker");
            var balance = new Label("₦0") { name = "nl-bank-balance" };
            balance.AddToClassList("nl-bank-balance");
            var syncRow = new VisualElement();
            syncRow.AddToClassList("nl-bank-sync-row");
            var sync = new Label("") { name = "nl-bank-sync" };
            sync.AddToClassList("nl-text-body");
            var refresh = new Button() { name = "nl-bank-refresh" };
            refresh.AddToClassList("nl-bank-refresh");
            var refreshIcon = new VisualElement();
            refreshIcon.AddToClassList("nl-icon");
            NLIcon.Set(refreshIcon, "refresh");
            var refreshLabel = new Label("Refresh");
            refreshLabel.AddToClassList("nl-text-body");
            refresh.Add(refreshIcon);
            refresh.Add(refreshLabel);
            syncRow.Add(sync);
            syncRow.Add(refresh);
            card.Add(kicker);
            card.Add(balance);
            card.Add(syncRow);
            root.Add(card);

            var tabs = new VisualElement();
            tabs.AddToClassList("nl-bank-tabs");
            var historyTab = new Button() { name = "nl-bank-tab-history" };
            historyTab.AddToClassList("nl-bank-tab");
            historyTab.AddToClassList("nl-bank-tab--active");
            var historyLabel = new Label("History") { name = "nl-bank-tab-history-label" };
            historyLabel.AddToClassList("nl-text-value");
            historyTab.Add(historyLabel);
            var weekTab = new Button() { name = "nl-bank-tab-week" };
            weekTab.AddToClassList("nl-bank-tab");
            var weekLabel = new Label("This week") { name = "nl-bank-tab-week-label" };
            weekLabel.AddToClassList("nl-text-value");
            weekTab.Add(weekLabel);
            tabs.Add(historyTab);
            tabs.Add(weekTab);
            root.Add(tabs);

            var scroll = new ScrollView();
            scroll.AddToClassList("nl-bank-scroll");
            var list = new VisualElement() { name = "nl-bank-list" };
            list.AddToClassList("nl-bank-list");
            scroll.Add(list);
            root.Add(scroll);

            var state = new VisualElement() { name = "nl-bank-state" };
            state.AddToClassList("nl-bank-state");
            state.style.display = DisplayStyle.None;
            var stateLabel = new Label("") { name = "nl-bank-state-label" };
            stateLabel.AddToClassList("nl-text-body");
            var retry = new Button() { name = "nl-bank-retry", text = "Retry" };
            retry.AddToClassList("nl-btn");
            retry.AddToClassList("nl-btn--ghost");
            retry.style.display = DisplayStyle.None;
            state.Add(stateLabel);
            state.Add(retry);
            root.Add(state);

            var loadMore = new Button() { name = "nl-bank-load-more", text = "Load more" };
            loadMore.AddToClassList("nl-btn");
            loadMore.AddToClassList("nl-btn--ghost");
            loadMore.style.display = DisplayStyle.None;
            root.Add(loadMore);

            var lockNote = new VisualElement();
            lockNote.AddToClassList("nl-bank-lock");
            var lockIcon = new VisualElement();
            lockIcon.AddToClassList("nl-icon");
            NLIcon.Set(lockIcon, "lock");
            var lockLabel = new Label(NLStrings.BankSendLocked);
            lockLabel.AddToClassList("nl-text-body");
            lockNote.Add(lockIcon);
            lockNote.Add(lockLabel);
            root.Add(lockNote);

            return root;
        }

        private VisualElement BuildMapScreen()
        {
            var root = new VisualElement();
            root.AddToClassList("nl-phone-page");
            root.style.display = DisplayStyle.None;
            var stub = new VisualElement();
            stub.AddToClassList("nl-map-stub");
            var wrap = new VisualElement();
            wrap.AddToClassList("nl-radar-wrap");
            var disc = new VisualElement();
            disc.AddToClassList("nl-radar");
            wrap.Add(disc);
            var title = new Label("Map");
            title.AddToClassList("nl-text-title");
            var body = new Label("The full Lagos map arrives with the M2 city. The radar on the HUD shows where you are now.");
            body.AddToClassList("nl-text-body");
            stub.Add(wrap);
            stub.Add(title);
            stub.Add(body);
            root.Add(stub);
            return root;
        }

        private VisualElement BuildSettingsScreen()
        {
            var root = new VisualElement();
            root.AddToClassList("nl-phone-page");
            root.style.display = DisplayStyle.None;
            var list = new VisualElement() { name = "nl-settings-list" };
            root.Add(list);
            return root;
        }

        private void BuildSettingsModel()
        {
            var model = new SettingsScreenModel();
            model.Items.Add(new NLSettingsScreen.SettingItem
            {
                Title = "Reduce motion",
                Subtitle = "Calmer animations across the HUD and phone.",
                IsToggle = true,
                ToggleValue = _uiRoot.ReduceMotion,
                OnToggle = v => _uiRoot.SetReduceMotion(v)
            });
            model.Items.Add(new NLSettingsScreen.SettingItem
            {
                Title = "Sound",
                Subtitle = "Game sound effects.",
                IsToggle = true,
                ToggleValue = PlayerPrefs.GetInt("nl_sound", 1) == 1,
                OnToggle = v =>
                {
                    PlayerPrefs.SetInt("nl_sound", v ? 1 : 0);
                    AudioListener.volume = v ? 1f : 0f;
                }
            });
            model.Items.Add(new NLSettingsScreen.SettingItem
            {
                Title = "Music",
                Subtitle = "In-game music.",
                IsToggle = true,
                ToggleValue = PlayerPrefs.GetInt("nl_music", 1) == 1,
                OnToggle = v => PlayerPrefs.SetInt("nl_music", v ? 1 : 0)
            });
            _settings.Build(model);
        }
    }
}
