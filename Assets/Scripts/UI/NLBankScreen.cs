// Naija Streets: Bank app, wallet from the server ledger (HUD-6).
// Spec docs/specs/hud-and-phone.md 8.3.
//   - Balance card (full format), "Synced N min ago", Refresh.
//   - Tabs: History, This week. The Send tab stays HIDDEN until B-38 is live
//     and the two-account test passes (owner gate, TASKS.md B-38).
//   - States: loading (skeleton within 300 ms), slow (3 s), timeout (8 s, last
//     known balance + Retry), empty history.
//   - The client never changes money; it only shows what the server said.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public sealed class NLBankScreen
    {
        private const int PageSize = 30;
        private const long SkeletonDelayMs = 300, SlowMs = 3000, TimeoutMs = 8000;

        private VisualElement _root, _list, _week, _stateBox, _refresh, _testTag;
        private Label _value, _sync, _stateLine;
        private Button _retry, _more, _tabHistory, _tabWeek;
        private readonly List<WalletEntry> _rows = new List<WalletEntry>();
        private int _request;          // drops answers from an older request
        private bool _historyTab = true;
        private bool _visible;
        private float _nextSyncLine;

        public VisualElement Build()
        {
            _root = new VisualElement();
            _root.AddToClassList("nl-bank");
            _root.style.flexGrow = 1;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            _root.Add(scroll);
            var c = scroll.contentContainer;

            // balance card
            var card = NLPhone.Add(c, "nl-balance");
            var k = new Label("YOUR WALLET"); k.AddToClassList("nl-balance-kicker"); card.Add(k);
            _value = new Label(); _value.AddToClassList("nl-balance-value"); card.Add(_value);
            var foot = NLPhone.Add(card, "nl-balance-foot");
            _sync = new Label(); _sync.AddToClassList("nl-balance-sync"); foot.Add(_sync);
            _refresh = NLPhone.Add(foot, "nl-refresh");
            NLPhone.Icon(_refresh, "refresh");
            var rl = new Label("Refresh"); rl.AddToClassList("nl-refresh-label"); _refresh.Add(rl);
            _refresh.RegisterCallback<ClickEvent>(_ => { NLDevice.Tap(); Load(); });
            _testTag = new Label("TEST MONEY, EDITOR ONLY"); _testTag.AddToClassList("nl-test-money"); card.Add(_testTag);

            // tabs (no Send tab: B-38 owner gate)
            var tabs = NLPhone.Add(c, "nl-tabs");
            _tabHistory = new Button(() => SetTab(true)) { text = "History" }; _tabHistory.AddToClassList("nl-tab"); tabs.Add(_tabHistory);
            _tabWeek = new Button(() => SetTab(false)) { text = "This week" }; _tabWeek.AddToClassList("nl-tab"); tabs.Add(_tabWeek);

            _list = NLPhone.Add(c, "nl-list");
            _week = NLPhone.Add(c, "nl-week");
            _stateBox = NLPhone.Add(c, "nl-state");
            _stateLine = new Label(); _stateLine.AddToClassList("nl-state-line"); _stateBox.Add(_stateLine);
            _retry = new Button(Load) { text = "Retry" }; _retry.AddToClassList("nl-btn"); _stateBox.Add(_retry);
            _more = new Button(LoadMore) { text = "Load more" }; _more.AddToClassList("nl-btn"); _more.AddToClassList("nl-btn--ghost"); c.Add(_more);

            var note = NLPhone.Add(c, "nl-note");
            NLPhone.Icon(note, "lock");
            var nl = new Label("Every naira here na game money. E no fit cash out."); nl.AddToClassList("nl-note-line"); note.Add(nl);

            SetTab(true);
            return _root;
        }

        public void OnShow()
        {
            _visible = true;
            NLWallet.BalanceChanged += OnBalance;
            PaintBalance();
            Load();
        }

        public void OnHide()
        {
            _visible = false;
            NLWallet.BalanceChanged -= OnBalance;
            _request++; // ignore answers that land after we left
        }

        public void Tick()
        {
            if (!_visible || Time.unscaledTime < _nextSyncLine) return;
            _nextSyncLine = Time.unscaledTime + 15f;
            _sync.text = NLWallet.SyncedLine();
        }

        private void OnBalance(long oldV, long newV, string reason)
        {
            PaintBalance();
            if (_visible) LoadHistory(0, replace: true, req: _request); // a money event landed: new row
        }

        private void PaintBalance()
        {
            bool has = NLWallet.HasBalance;
            _value.text = has ? NLMoney.Full(NLWallet.Balance) : NLMoney.Sign + "...";
            _value.EnableInClassList("is-loading", !has);
            _sync.text = NLWallet.SyncedLine();
            _testTag.style.display = NLWallet.Source != null && NLWallet.Source.IsTestMoney ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetTab(bool history)
        {
            _historyTab = history;
            _tabHistory.EnableInClassList("is-on", history);
            _tabWeek.EnableInClassList("is-on", !history);
            _list.style.display = history ? DisplayStyle.Flex : DisplayStyle.None;
            _week.style.display = history ? DisplayStyle.None : DisplayStyle.Flex;
            RenderWeek();
            RefreshMoreButton();
        }

        // ---------------- loading ----------------

        private void Load()
        {
            int req = ++_request;
            _refresh.AddToClassList("is-busy");
            HideState();

            // skeleton after 300 ms, "still loading" at 3 s, friendly timeout at 8 s
            _root.schedule.Execute(() => { if (req == _request && _rows.Count == 0) ShowSkeleton(); }).StartingIn(SkeletonDelayMs);
            _root.schedule.Execute(() => { if (req == _request) ShowState("Still loading your money...", retry: false); }).StartingIn(SlowMs);
            _root.schedule.Execute(() =>
            {
                if (req != _request) return;
                _request++;
                _refresh.RemoveFromClassList("is-busy");
                ShowState(NLWallet.HasBalance ? "No network. Showing your last balance." : "No network. Check your data and try again.", retry: true);
            }).StartingIn(TimeoutMs);

            if (NLWallet.Source == null)
            {
                // economy service not connected yet (before M5/M6 in a player build)
                _request++;
                _refresh.RemoveFromClassList("is-busy");
                ShowState("Bank go open once you sign in.", retry: false);
                return;
            }

            NLWallet.Refresh(status =>
            {
                if (req != _request) return;
                PaintBalance();
                if (status != WalletStatus.Ok)
                {
                    _request++;
                    _refresh.RemoveFromClassList("is-busy");
                    ShowState("No network. Showing your last balance.", retry: true);
                    return;
                }
                LoadHistory(0, replace: true, req: req);
            });
        }

        private void LoadMore() => LoadHistory(_rows.Count, replace: false, req: ++_request);

        private void LoadHistory(int offset, bool replace, int req)
        {
            if (NLWallet.Source == null) return;
            NLWallet.Source.FetchHistory(offset, PageSize, (status, page) =>
            {
                if (req != _request) return;
                _request++; // this request is finished; stop its slow/timeout timers
                _refresh.RemoveFromClassList("is-busy");
                if (status != WalletStatus.Ok) { ShowState("No network. Showing your last balance.", retry: true); return; }
                HideState();
                if (replace) _rows.Clear();
                _rows.AddRange(page);
                _lastPageFull = page.Count >= PageSize;
                RenderRows();
                RenderWeek();
                RefreshMoreButton();
            });
        }

        private bool _lastPageFull;

        private void RefreshMoreButton()
        {
            _more.style.display = _historyTab && _lastPageFull ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------- render ----------------

        private void ShowSkeleton()
        {
            _list.Clear();
            for (int i = 0; i < 4; i++)
            {
                var r = NLPhone.Add(_list, "nl-skel");
                NLPhone.Add(r, "nl-skel-tile");
                NLPhone.Add(r, "nl-skel-bar");
            }
        }

        private void RenderRows()
        {
            _list.Clear();
            if (_rows.Count == 0)
            {
                ShowState("No money moves yet. Your first shift go show here.", retry: false);
                return;
            }
            int i = 0;
            foreach (var row in _rows)
            {
                var r = NLPhone.Add(_list, "nl-txn");
                var tile = NLPhone.Add(r, "nl-icon-tile");
                NLPhone.Icon(tile, IconFor(row.Source));
                var text = NLPhone.Add(r, "nl-txn-text");
                var t = new Label(row.Title); t.AddToClassList("nl-txn-title"); text.Add(t);
                var l = new Label(Line(row)); l.AddToClassList("nl-txn-line"); text.Add(l);
                var a = new Label(NLMoney.Delta(row.Amount)); a.AddToClassList("nl-txn-amount");
                a.EnableInClassList("is-in-money", row.Amount > 0);
                r.Add(a);
                // rows rise in one after another
                long delay = NLUIRoot.ReducedMotion ? 0 : 16 + Math.Min(i, 8) * 30;
                r.schedule.Execute(() => r.AddToClassList("is-in")).StartingIn(delay);
                i++;
            }
        }

        private void RenderWeek()
        {
            if (_week == null) return;
            _week.Clear();
            // WAT week starts Monday 00:00 WAT
            var wat = NLHud.WatNow;
            int back = ((int)wat.DayOfWeek + 6) % 7;
            DateTime startUtc = wat.Date.AddDays(-back).AddHours(-1);
            long income = 0, spent = 0;
            foreach (var r in _rows)
            {
                if (r.TimeUtc < startUtc) continue;
                if (r.Amount > 0) income += r.Amount; else spent += -r.Amount;
            }
            WeekRow("Money in", NLMoney.Full(income), true);
            WeekRow("Money out", NLMoney.Full(spent), false);
            WeekRow("Net", NLMoney.Delta(income - spent), income - spent > 0);
        }

        private void WeekRow(string label, string value, bool green)
        {
            var row = NLPhone.Add(_week, "nl-week-row");
            var l = new Label(label); l.AddToClassList("nl-week-label"); row.Add(l);
            var v = new Label(value); v.AddToClassList("nl-week-value"); row.Add(v);
            if (green) v.style.color = new StyleColor(new Color32(0x2E, 0xE5, 0x9D, 0xFF));
        }

        private void ShowState(string line, bool retry)
        {
            _stateBox.style.display = DisplayStyle.Flex;
            _stateLine.text = line;
            _retry.style.display = retry ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HideState() => _stateBox.style.display = DisplayStyle.None;

        private static string Line(WalletEntry r)
        {
            var wat = r.TimeUtc.AddHours(1);
            var today = NLHud.WatNow.Date;
            string when = wat.Date == today ? wat.ToString("HH:mm")
                        : wat.Date == today.AddDays(-1) ? "Yesterday " + wat.ToString("HH:mm")
                        : wat.ToString("ddd HH:mm");
            return string.IsNullOrEmpty(r.Place) ? when : r.Place + "   " + when;
        }

        private static string IconFor(string source)
        {
            switch (source)
            {
                case "job": return "briefcase";
                case "food": return "bowl";
                case "transport": return "bus";
                case "rent": return "home";
                case "transfer": return "send";
                case "start": return "coin";
                default: return "wallet";
            }
        }
    }
}
