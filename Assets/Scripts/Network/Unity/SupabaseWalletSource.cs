// Naija Streets (Unity) — HUD-6 live data: the real IWalletSource.
// Reads the player's OWN rows of the live economy_ledger table (row-level
// security "owners read": auth.uid() = user_id). Read only: the client never
// writes money; it shows what the server last said (NLWallet.AdoptServerBalance).
//   Balance = balance_after of the newest row (by seq). No rows = 0.
//   History = newest first, paged with offset/limit.
// Spec: docs/specs/hud-and-phone.md §8.3, docs/specs/netcode-m5.md §5.4.
// UNVERIFIED (2026-11-06): not compiled or run yet.

using System;
using System.Collections.Generic;
using System.Globalization;
using NaijaLife.UI;
using UnityEngine;

namespace NaijaLife.NetworkGlue
{
    public sealed class SupabaseWalletSource : IWalletSource
    {
        [Serializable] class Row { public string id; public long delta; public long balance_after; public string reason; public string source; public string ts; }
        [Serializable] class Rows { public Row[] items; }

        const string Select = "/rest/v1/economy_ledger?select=id,delta,balance_after,reason,source,ts&order=seq.desc";
        readonly SupabaseSession _sb;

        public SupabaseWalletSource(SupabaseSession sb) { _sb = sb ?? throw new ArgumentNullException(nameof(sb)); }

        public bool IsTestMoney => false;

        public async void FetchBalance(Action<WalletStatus, long> done)
        {
            var r = await _sb.GetAsync(Select + "&limit=1");
            if (!r.ok) { done?.Invoke(StatusOf(r.status), 0); return; }
            Row[] rows = Parse(r.body);
            if (rows == null) { done?.Invoke(WalletStatus.Error, 0); return; }
            done?.Invoke(WalletStatus.Ok, rows.Length == 0 ? 0 : rows[0].balance_after);
        }

        public async void FetchHistory(int offset, int count, Action<WalletStatus, IReadOnlyList<WalletEntry>> done)
        {
            offset = Math.Max(0, offset);
            count = Mathf.Clamp(count, 1, 100);
            var r = await _sb.GetAsync(Select + "&offset=" + offset + "&limit=" + count);
            if (!r.ok) { done?.Invoke(StatusOf(r.status), Array.Empty<WalletEntry>()); return; }
            Row[] rows = Parse(r.body);
            if (rows == null) { done?.Invoke(WalletStatus.Error, Array.Empty<WalletEntry>()); return; }
            var list = new List<WalletEntry>(rows.Length);
            foreach (Row row in rows) list.Add(ToEntry(row));
            done?.Invoke(WalletStatus.Ok, list);
        }

        static WalletStatus StatusOf(long http) => http == 0 ? WalletStatus.Offline : WalletStatus.Error;

        static Row[] Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<Rows>("{\"items\":" + json + "}")?.items; } // JsonUtility cannot read a bare array
            catch (Exception) { return null; }
        }

        /// <summary>Maps live ledger sources (nl_ledger_source_rules, checked 2026-11-06) to the Bank's groups.</summary>
        public static WalletEntry ToEntry(in RowView v)
        {
            string group, title;
            switch (v.Source)
            {
                case "job_payout":
                case "job_shift":       group = "job"; title = "Shift pay"; break;
                case "sale":            group = "job"; title = "Sale"; break;
                case "purchase":        group = "other"; title = "Purchase"; break;
                case "fine":            group = "other"; title = "Fine"; break;
                case "loan":            group = "other"; title = "Loan"; break;
                case "loan_repay":      group = "other"; title = "Loan repayment"; break;
                case "gift":            group = "transfer"; title = "Gift"; break;
                case "transfer_in":     group = "transfer"; title = "Money received"; break;
                case "transfer_out":    group = "transfer"; title = "Money sent"; break;
                case "starter_grant":
                case "opening_balance": group = "start"; title = "Starting money"; break;
                case "family_stipend":  group = "start"; title = "Family support"; break;
                case "pack_credit":     group = "other"; title = "Naira pack"; break;
                default:                group = "other"; title = "Payment"; break;
            }
            DateTime t;
            if (!DateTime.TryParse(v.Ts, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t)) t = DateTime.UtcNow;
            return new WalletEntry
            {
                Id = v.Id,
                Amount = v.Delta,
                Source = group,
                Title = title,
                Place = null, // the ledger has no place column yet; the Bank hides an empty line
                TimeUtc = t,
            };
        }

        static WalletEntry ToEntry(Row r) =>
            ToEntry(new RowView { Id = r.id, Delta = r.delta, Source = r.source, Ts = r.ts });

        /// <summary>Plain view of one ledger row (also used by tests).</summary>
        public struct RowView { public string Id; public long Delta; public string Source; public string Ts; }
    }
}
