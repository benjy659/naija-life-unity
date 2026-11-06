// Naija Streets (Unity rebuild) — NLBankScreen
// The Bank (wallet) app. Spec section 8.3-8.4. Balance card, History and
// This week tabs, rows by ledger source. Data comes from IWalletSource
// (fake source in the Editor until the M5/M6 economy service lands).
// States: loading (skeleton rows within 300 ms), slow (3 s), timeout
// (8 s, last known balance + Retry), empty history. Send tab stays hidden
// (B-38 owner gate).

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NaijaLife.UI
{
    public class NLBankScreen
    {
        private const int PageSize = 30;

        private readonly IWalletSource _source;
        private readonly VisualElement _root;
        private readonly Label _balanceLabel;
        private readonly Label _syncLabel;
        private readonly Button _refreshButton;
        private readonly Button _historyTab;
        private readonly Button _weekTab;
        private readonly VisualElement _list;
        private readonly VisualElement _stateBox;
        private readonly Label _stateLabel;
        private readonly Button _retryButton;
        private readonly Button _loadMoreButton;

        private bool _showingWeek;
        private int _page;
        private float _refreshStartedAt;
        private bool _refreshPending;
        private bool _slowShown;

        public NLBankScreen(IWalletSource source, VisualElement root)
        {
            _source = source;
            _root = root;
            _balanceLabel = root.Q<Label>("nl-bank-balance");
            _syncLabel = root.Q<Label>("nl-bank-sync");
            _refreshButton = root.Q<Button>("nl-bank-refresh");
            _historyTab = root.Q<Button>("nl-bank-tab-history");
            _weekTab = root.Q<Button>("nl-bank-tab-week");
            _list = root.Q<VisualElement>("nl-bank-list");
            _stateBox = root.Q<VisualElement>("nl-bank-state");
            _stateLabel = root.Q<Label>("nl-bank-state-label");
            _retryButton = root.Q<Button>("nl-bank-retry");
            _loadMoreButton = root.Q<Button>("nl-bank-load-more");

            if (_refreshButton != null) _refreshButton.clicked += () => Refresh();
            if (_historyTab != null) _historyTab.clicked += () => ShowTab(false);
            if (_weekTab != null) _weekTab.clicked += () => ShowTab(true);
            if (_retryButton != null) _retryButton.clicked += () => Refresh();
            if (_loadMoreButton != null) _loadMoreButton.clicked += () => { _page++; RenderList(); };
            _source.OnChanged += Render;
        }

        public VisualElement Root => _root;

        public void OnShow()
        {
            Refresh();
            Render();
        }

        public void Tick()
        {
            if (!_refreshPending) return;
            // realtime: the game is paused while the phone is open.
            float elapsed = Time.realtimeSinceStartup - _refreshStartedAt;
            if (!_slowShown && elapsed > 3f && _source.IsLoading)
            {
                _slowShown = true;
                ShowState(NLStrings.BankLoading, false);
            }
            if (elapsed > 8f && _source.IsLoading)
            {
                _refreshPending = false;
                ShowState(NLStrings.BankOffline, true);
                RenderBalance(); // last known balance stays visible
            }
        }

        public void Refresh()
        {
            _refreshStartedAt = Time.realtimeSinceStartup;
            _refreshPending = true;
            _slowShown = false;
            ShowLoadingSkeletons();
            _source.Refresh(success =>
            {
                _refreshPending = false;
                if (success) Render();
                else ShowState(NLStrings.BankOffline, true);
            });
        }

        private void Render()
        {
            RenderBalance();
            RenderList();
        }

        private void RenderBalance()
        {
            if (_balanceLabel != null)
                _balanceLabel.text = NLMoney.Full(_source.Balance);
            if (_syncLabel != null)
            {
                TimeSpan ago = DateTime.UtcNow - _source.LastSyncedUtc;
                string agoText = ago.TotalMinutes < 1 ? "just now"
                    : ago.TotalMinutes < 60 ? $"{(int)ago.TotalMinutes} min ago"
                    : $"{(int)ago.TotalHours} hr ago";
                _syncLabel.text = string.Format(NLStrings.BankSynced, agoText);
            }
        }

        private void ShowTab(bool week)
        {
            _showingWeek = week;
            _page = 0;
            _historyTab?.EnableInClassList("nl-bank-tab--active", !week);
            _weekTab?.EnableInClassList("nl-bank-tab--active", week);
            RenderList();
        }

        private void RenderList()
        {
            if (_list == null) return;
            _list.Clear();
            if (_stateBox != null) _stateBox.style.display = DisplayStyle.None;
            if (_loadMoreButton != null) _loadMoreButton.style.display = DisplayStyle.None;

            if (_showingWeek)
            {
                var w = _source.GetWeekSummary();
                AddWeekRow(NLStrings.BankMoneyIn, w.MoneyIn, true);
                AddWeekRow(NLStrings.BankMoneyOut, w.MoneyOut, false);
                AddWeekRow(NLStrings.BankNet, w.Net, w.Net >= 0);
                return;
            }

            var rows = _source.GetHistory(_page, PageSize);
            if (rows.Count == 0 && _page == 0)
            {
                ShowState(NLStrings.BankEmpty, false);
                return;
            }
            foreach (var e in rows) AddHistoryRow(e);
            if (_loadMoreButton != null && _source.HistoryCount > (_page + 1) * PageSize)
                _loadMoreButton.style.display = DisplayStyle.Flex;
        }

        private void AddWeekRow(string label, long amount, bool isIn)
        {
            var row = new VisualElement();
            row.AddToClassList("nl-bank-week-row");
            var name = new Label(label);
            name.AddToClassList("nl-text-body");
            var value = new Label((isIn ? "+" : "-") + NLMoney.Full(Math.Abs(amount)));
            value.AddToClassList("nl-bank-amount");
            value.EnableInClassList("nl-bank-amount--in", isIn);
            row.Add(name);
            row.Add(value);
            _list.Add(row);
        }

        private void AddHistoryRow(LedgerEntry e)
        {
            var row = new VisualElement();
            row.AddToClassList("nl-bank-row");
            var tile = new VisualElement();
            tile.AddToClassList("nl-icon-tile");
            var icon = new VisualElement();
            icon.AddToClassList("nl-icon");
            NLIcon.Set(icon, IconFor(e.Source));
            tile.Add(icon);
            var texts = new VisualElement();
            texts.AddToClassList("nl-bank-row-texts");
            var title = new Label(e.Title);
            title.AddToClassList("nl-text-value");
            var sub = new Label($"{e.Place}  {ToWatTime(e.Time)}");
            sub.AddToClassList("nl-text-body");
            texts.Add(title);
            texts.Add(sub);
            bool isIn = e.Amount >= 0;
            var amount = new Label((isIn ? "+" : "-") + NLMoney.Full(Math.Abs(e.Amount)));
            amount.AddToClassList("nl-bank-amount");
            amount.EnableInClassList("nl-bank-amount--in", isIn);
            row.Add(tile);
            row.Add(texts);
            row.Add(amount);
            _list.Add(row);
        }

        private void ShowLoadingSkeletons()
        {
            if (_list == null) return;
            _list.Clear();
            for (int i = 0; i < 3; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("nl-bank-row");
                var tile = new VisualElement();
                tile.AddToClassList("nl-icon-tile");
                var texts = new VisualElement();
                texts.AddToClassList("nl-bank-row-texts");
                texts.Add(new Label("Loading") { name = "nl-skeleton" });
                row.Add(tile);
                row.Add(texts);
                _list.Add(row);
            }
            if (_stateBox != null) _stateBox.style.display = DisplayStyle.None;
        }

        private void ShowState(string message, bool showRetry)
        {
            if (_stateBox == null) return;
            _stateBox.style.display = DisplayStyle.Flex;
            if (_stateLabel != null) _stateLabel.text = message;
            if (_retryButton != null) _retryButton.style.display = showRetry ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string IconFor(LedgerSourceKind source)
        {
            switch (source)
            {
                case LedgerSourceKind.Job: return "briefcase";
                case LedgerSourceKind.Food: return "bowl";
                case LedgerSourceKind.Transport: return "bus";
                case LedgerSourceKind.Rent: return "home";
                case LedgerSourceKind.Transfer: return "send";
                case LedgerSourceKind.Start: return "coin";
                default: return "coin";
            }
        }

        private static string ToWatTime(DateTime utc)
        {
            // WAT is UTC+1, no daylight saving.
            DateTime wat = utc.AddHours(1);
            return wat.ToString("HH:mm");
        }
    }
}
