// Naija Streets: the client wallet is a READ CACHE of the server ledger.
// Spec: docs/specs/hud-and-phone.md section 8.3, design inventory section 7.
//   - The client never adds or subtracts money. It shows what the server last
//     said (AdoptServerBalance) and asks again on open, after money events,
//     on Refresh, and every 5 minutes.
//   - The economy service (M5/M6) registers the real IWalletSource. Until then
//     the Editor uses NLEditorFakeWallet, which is compiled OUT of every player
//     build: a release build never shows fake money.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NaijaLife.UI
{
    public enum WalletStatus { Ok, Offline, Error }

    /// <summary>One ledger row as the Bank shows it.</summary>
    public struct WalletEntry
    {
        public string Id;          // ledger row id
        public long Amount;        // + money in, - money out, whole naira
        public string Source;      // job, food, transport, rent, transfer, start, other
        public string Title;       // "Conductor shift"
        public string Place;       // "Oshodi to Ikeja"
        public DateTime TimeUtc;
    }

    public interface IWalletSource
    {
        /// <summary>True only for the Editor fake source; the Bank shows a TEST MONEY tag.</summary>
        bool IsTestMoney { get; }
        void FetchBalance(Action<WalletStatus, long> done);
        void FetchHistory(int offset, int count, Action<WalletStatus, IReadOnlyList<WalletEntry>> done);
    }

    public static class NLWallet
    {
        public const float PollSeconds = 300f; // 5 minutes (web DB polling rule)

        private static IWalletSource _source;
        public static IWalletSource Source
        {
            get => _source;
            set { _source = value; HasBalance = false; SourceChanged?.Invoke(); }
        }

        public static bool HasBalance { get; private set; }
        public static long Balance { get; private set; }
        public static DateTime LastSyncUtc { get; private set; }
        public static bool Busy { get; private set; }
        public static WalletStatus LastStatus { get; private set; } = WalletStatus.Ok;

        /// <summary>(old, new, reason). reason is a short player line or null.</summary>
        public static event Action<long, long, string> BalanceChanged;
        public static event Action SourceChanged;
        public static event Action<WalletStatus> RefreshFinished;

        /// <summary>The ONLY way the shown balance changes: the server said so.</summary>
        public static void AdoptServerBalance(long balance, string reason = null)
        {
            long old = Balance;
            bool had = HasBalance;
            Balance = balance;
            HasBalance = true;
            LastSyncUtc = DateTime.UtcNow;
            LastStatus = WalletStatus.Ok;
            if (!had || old != balance) BalanceChanged?.Invoke(had ? old : balance, balance, had ? reason : null);
        }

        public static void Refresh(Action<WalletStatus> done = null)
        {
            if (_source == null) { done?.Invoke(WalletStatus.Offline); return; }
            if (Busy) { if (done != null) RefreshFinished += Once(done); return; }
            Busy = true;
            var asked = _source;
            asked.FetchBalance((status, balance) =>
            {
                Busy = false;
                if (asked != _source) return; // source swapped while we waited
                LastStatus = status;
                if (status == WalletStatus.Ok) AdoptServerBalance(balance);
                done?.Invoke(status);
                RefreshFinished?.Invoke(status);
            });
        }

        private static Action<WalletStatus> Once(Action<WalletStatus> cb)
        {
            Action<WalletStatus> h = null;
            h = s => { RefreshFinished -= h; cb(s); };
            return h;
        }

        /// <summary>"Synced 2 min ago" style line. No raw separators.</summary>
        public static string SyncedLine()
        {
            if (!HasBalance) return "Not synced yet";
            var age = DateTime.UtcNow - LastSyncUtc;
            if (age.TotalSeconds < 45) return "Synced just now";
            if (age.TotalMinutes < 60) return "Synced " + Mathf.Max(1, (int)age.TotalMinutes) + " min ago";
            return "Synced " + (int)age.TotalHours + " h ago";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _source = null; HasBalance = false; Balance = 0; Busy = false;
            BalanceChanged = null; SourceChanged = null; RefreshFinished = null;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only stand-in for the economy service so the HUD and Bank can be
    /// seen before M5/M6. Never compiled into a player build.
    /// Menu: Naija Streets > UI > Test: pay a shift (₦1,200) / spend at Mama Put.
    /// </summary>
    public sealed class NLEditorFakeWallet : IWalletSource
    {
        public static NLEditorFakeWallet Current { get; private set; }

        public bool IsTestMoney => true;
        public float LatencySeconds = 0.6f;
        public bool SimulateOffline;

        private long _balance = 8450;
        private readonly List<WalletEntry> _rows = new List<WalletEntry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (NLWallet.Source != null) return;
            Current = new NLEditorFakeWallet();
            NLWallet.Source = Current;
            NLWallet.Refresh();
        }

        private NLEditorFakeWallet()
        {
            var now = DateTime.UtcNow;
            _rows.Add(Row(1200, "job", "Conductor shift", "Oshodi to Ikeja", now.AddMinutes(-20)));
            _rows.Add(Row(-1200, "food", "Mama Put jollof", "Oshodi", now.AddMinutes(-35)));
            _rows.Add(Row(-300, "transport", "Danfo fare", "Oshodi", now.AddMinutes(-59)));
            _rows.Add(Row(8750, "start", "Start money", "Lapo start", now.AddHours(-14)));
        }

        private static WalletEntry Row(long amt, string src, string title, string place, DateTime t) =>
            new WalletEntry { Id = Guid.NewGuid().ToString("N"), Amount = amt, Source = src, Title = title, Place = place, TimeUtc = t };

        public void FetchBalance(Action<WalletStatus, long> done) =>
            Later(() => done(SimulateOffline ? WalletStatus.Offline : WalletStatus.Ok, _balance));

        public void FetchHistory(int offset, int count, Action<WalletStatus, IReadOnlyList<WalletEntry>> done) =>
            Later(() =>
            {
                if (SimulateOffline) { done(WalletStatus.Offline, Array.Empty<WalletEntry>()); return; }
                var page = new List<WalletEntry>();
                for (int i = offset; i < _rows.Count && page.Count < count; i++) page.Add(_rows[i]);
                done(WalletStatus.Ok, page);
            });

        /// <summary>Pretend the server paid or charged the player (Editor test only).</summary>
        public void ServerEvent(long amount, string source, string title, string place, string toastLine)
        {
            _balance += amount;
            _rows.Insert(0, Row(amount, source, title, place, DateTime.UtcNow));
            Later(() => NLWallet.AdoptServerBalance(_balance, toastLine));
        }

        private async void Later(Action a)
        {
            await System.Threading.Tasks.Task.Delay(Mathf.RoundToInt(LatencySeconds * 1000f));
            if (Application.isPlaying) a();
        }
    }
#endif
}
