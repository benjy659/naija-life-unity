// Naija Streets (Unity rebuild) — fake wallet source.
// EDITOR-ONLY stand-in for the server ledger, used by the HUD wallet chip
// and the Bank app until the M5/M6 economy service lands.
// Compiled out of player builds (UNITY_EDITOR guard in NLHudController) —
// no fake money in a player build, ever. Values match the spec mockups
// (balance ₦8,450; the four history rows from the Bank mockup).

using System;
using System.Collections.Generic;

namespace NaijaLife.UI
{
    public sealed class FakeWalletSource : IWalletSource
    {
        private readonly List<LedgerEntry> _history = new List<LedgerEntry>();
        private DateTime _lastSyncedUtc;
        private bool _isLoading;

        public event Action OnChanged;

        public FakeWalletSource()
        {
            DateTime now = DateTime.UtcNow;
            // Times are WAT (UTC+1) in the UI; stored here as UTC.
            _history.Add(new LedgerEntry
            {
                Id = "f1", Title = "Conductor shift", Place = "Oshodi to Ikeja",
                Time = now.AddMinutes(-32), Amount = 1200, Source = LedgerSourceKind.Job
            });
            _history.Add(new LedgerEntry
            {
                Id = "f2", Title = "Mama Put jollof", Place = "Oshodi",
                Time = now.AddMinutes(-47), Amount = -1200, Source = LedgerSourceKind.Food
            });
            _history.Add(new LedgerEntry
            {
                Id = "f3", Title = "Danfo fare", Place = "Oshodi",
                Time = now.AddHours(-1).AddMinutes(-11), Amount = -300, Source = LedgerSourceKind.Transport
            });
            _history.Add(new LedgerEntry
            {
                Id = "f4", Title = "Start money", Place = "Lapo start",
                Time = now.AddDays(-1).AddHours(-2), Amount = 8000, Source = LedgerSourceKind.Start
            });
            _lastSyncedUtc = now.AddMinutes(-2);
        }

        public long Balance => 8450;
        public DateTime LastSyncedUtc => _lastSyncedUtc;
        public bool IsLoading => _isLoading;
        public bool HasError => false;
        public int HistoryCount => _history.Count;

        public IReadOnlyList<LedgerEntry> GetHistory(int page, int pageSize)
        {
            int start = page * pageSize;
            if (start >= _history.Count) return Array.Empty<LedgerEntry>();
            int count = Math.Min(pageSize, _history.Count - start);
            return _history.GetRange(start, count);
        }

        public WeekSummary GetWeekSummary()
        {
            long inn = 0, outt = 0;
            foreach (var e in _history)
            {
                if (e.Amount >= 0) inn += e.Amount;
                else outt += -e.Amount;
            }
            return new WeekSummary { MoneyIn = inn, MoneyOut = outt };
        }

        public void Refresh(Action<bool> onDone)
        {
            // Fake refresh: pretend the server answered at once.
            _isLoading = false;
            _lastSyncedUtc = DateTime.UtcNow;
            OnChanged?.Invoke();
            onDone?.Invoke(true);
        }
    }
}
