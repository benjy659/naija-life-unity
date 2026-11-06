// Naija Streets (Unity rebuild) — wallet data source.
// Spec: docs/specs/hud-and-phone.md section 8.3. The wallet is a READ CACHE
// of the server ledger. The client never adds or subtracts money itself; it
// shows what the server last said and adopts the server balance.
// The live source arrives with the M5/M6 economy service. Until then the
// HUD and Bank use the Editor-only fake source (compiled out of player
// builds — no fake money in a player build, ever).

using System;
using System.Collections.Generic;

namespace NaijaLife.UI
{
    public enum LedgerSourceKind { Job, Food, Transport, Rent, Transfer, Start, Other }

    public struct LedgerEntry
    {
        public string Id;
        public string Title;      // "Conductor shift"
        public string Place;      // "Oshodi to Ikeja"
        public DateTime Time;     // WAT time of the move
        public long Amount;       // signed naira; positive = money in
        public LedgerSourceKind Source;
    }

    public struct WeekSummary
    {
        public long MoneyIn;
        public long MoneyOut;     // positive magnitude
        public long Net => MoneyIn - MoneyOut;
    }

    public interface IWalletSource
    {
        /// <summary>Last known balance from the server (whole naira).</summary>
        long Balance { get; }

        /// <summary>When the balance was last refreshed from the server (UTC).</summary>
        DateTime LastSyncedUtc { get; }

        /// <summary>True while a refresh is in flight.</summary>
        bool IsLoading { get; }

        /// <summary>True when the last refresh failed (offline/timeout).</summary>
        bool HasError { get; }

        /// <summary>Newest-first ledger history, pageSize rows per page.</summary>
        IReadOnlyList<LedgerEntry> GetHistory(int page, int pageSize);

        /// <summary>Total history rows available.</summary>
        int HistoryCount { get; }

        /// <summary>This WAT week's money in / out.</summary>
        WeekSummary GetWeekSummary();

        /// <summary>Refresh from the server. Calls back on the main thread.</summary>
        void Refresh(Action<bool> onDone);

        /// <summary>Fired after a successful refresh (balance or history changed).</summary>
        event Action OnChanged;
    }
}
