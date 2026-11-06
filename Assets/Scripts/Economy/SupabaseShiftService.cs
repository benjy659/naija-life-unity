// Naija Streets (Unity) - IShiftService over the nl_shift_* RPCs
// (docs/specs/jobs-m4.md section 9; migration supabase/migrations/drafts/m4_job_shifts.sql).
// The player's own JWT is the bearer; the anon key is only the apikey.
// UNVERIFIED (2026-11-06): not compiled or run yet, and the RPCs are not live
// until Ted's team applies the migration.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NaijaLife.Jobs;
using NaijaLife.NetworkGlue;
using UnityEngine;
using UnityEngine.Networking;

namespace NaijaLife.Economy
{
    public sealed class SupabaseShiftService : IShiftService
    {
        [Serializable] class StartReq { public string p_job; public string p_slot; public int p_pawa; public string p_ref; }
        [Serializable] class TripReq { public string p_shift; public int p_seats; public int p_right; public int p_total; public string p_ref; }
        [Serializable] class EndReq { public string p_shift; public string p_reason; }
        [Serializable] class Reply
        {
            public bool ok; public string error;
            public string shift_id; public int shifts_today; public string ends_by;
            public int trip_no; public long trip_pay; public long shift_pending;
            public long paid; public bool voided; public long balance_after;
        }

        readonly SupabaseConfig _cfg;
        readonly SupabaseSession _session;

        public bool IsTestStub => false;

        public SupabaseShiftService(SupabaseConfig cfg, SupabaseSession session) { _cfg = cfg; _session = session; }

        public async Task<ShiftStart> StartShiftAsync(JobKind job, string slotId, int pawa, string clientRef, CancellationToken cancel)
        {
            Reply r = await Call("nl_shift_start", JsonUtility.ToJson(new StartReq { p_job = JobKindNames.Id(job), p_slot = slotId, p_pawa = pawa, p_ref = clientRef }), cancel);
            if (r == null) return new ShiftStart { Error = ShiftError.Network };
            DateTime.TryParse(r.ends_by, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime ends);
            return new ShiftStart { Ok = r.ok, Error = r.error, ShiftId = r.shift_id, ShiftsToday = r.shifts_today, EndsByUtc = ends };
        }

        public async Task<TripResult> RecordTripAsync(string shiftId, int seats, int faresRight, int faresTotal, string clientRef, CancellationToken cancel)
        {
            Reply r = await Call("nl_shift_trip", JsonUtility.ToJson(new TripReq { p_shift = shiftId, p_seats = seats, p_right = faresRight, p_total = faresTotal, p_ref = clientRef }), cancel);
            if (r == null) return new TripResult { Error = ShiftError.Network };
            return new TripResult { Ok = r.ok, Error = r.error, TripNo = r.trip_no, TripPay = r.trip_pay, ShiftPending = r.shift_pending };
        }

        public async Task<ShiftEnd> EndShiftAsync(string shiftId, string reason, CancellationToken cancel)
        {
            Reply r = await Call("nl_shift_end", JsonUtility.ToJson(new EndReq { p_shift = shiftId, p_reason = reason }), cancel);
            if (r == null) return new ShiftEnd { Error = ShiftError.Network };
            return new ShiftEnd { Ok = r.ok, Error = r.error, Paid = r.paid, Voided = r.voided, BalanceAfter = r.balance_after };
        }

        /// <summary>One RPC call. Null on network failure (the caller retries with the same reference).</summary>
        async Task<Reply> Call(string rpc, string json, CancellationToken cancel)
        {
            string token = await _session.GetAccessTokenAsync();
            if (token == null) return null;
            using (var req = new UnityWebRequest(_cfg.Url.TrimEnd('/') + "/rest/v1/rpc/" + rpc, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", _cfg.AnonKey);
                req.SetRequestHeader("Authorization", "Bearer " + token);
                req.timeout = 15;
                await req.SendWebRequest();
                if (cancel.IsCancellationRequested) return null;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Jobs] " + rpc + " failed: " + req.responseCode + " " + req.error);
                    return null;
                }
                return JsonUtility.FromJson<Reply>(req.downloadHandler.text);
            }
        }
    }
}
