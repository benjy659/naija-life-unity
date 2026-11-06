// Naija Streets (Unity) - in-memory copy of the server's shift rules
// (docs/specs/jobs-m4.md section 9). Used by LocalShiftService in the Editor
// and by the Edit Mode tests. The SQL in supabase/migrations is the real
// authority; this class must give the same answers for the same calls.

using System;
using System.Collections.Generic;

namespace NaijaLife.Jobs
{
    public sealed class ShiftLedger
    {
        sealed class Shift
        {
            public string Id, Job, Slot, ClientRef;
            public DateTime Started, LastTrip;
            public DateTime? Ended;
            public string Status = "open";   // open | paid | void
            public int Trips;
            public long Pending, Paid;
            public DateTime WatDay;
            public readonly Dictionary<string, TripResult> TripsByRef = new Dictionary<string, TripResult>();
            public readonly List<int> TripSeats = new List<int>();
            public ShiftEnd? Closed;
        }

        readonly Func<DateTime> _nowUtc;
        readonly List<Shift> _shifts = new List<Shift>();
        int _countedTrips;   // lifetime trips with seats >= 4 in paid shifts
        int _nextId;

        public long Balance { get; private set; }
        public int CountedTrips => _countedTrips;
        public int Level => ShiftRules.Level(_countedTrips);

        public ShiftLedger(Func<DateTime> nowUtc, long startBalance = 0)
        {
            _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
            Balance = startBalance;
        }

        public ShiftStart Start(string job, string slot, int pawa, string clientRef)
        {
            DateTime now = _nowUtc();
            LazyClose(now);
            if (job != "conductor" || string.IsNullOrEmpty(slot) || slot.Length > 64 || string.IsNullOrEmpty(clientRef))
                return new ShiftStart { Error = ShiftError.BadInput };
            foreach (Shift s in _shifts)
                if (s.ClientRef == clientRef)
                    return new ShiftStart { Ok = true, ShiftId = s.Id, ShiftsToday = PaidToday(now), EndsByUtc = s.Started.AddMinutes(ShiftRules.ShiftMinutes) };
            if (pawa < ShiftRules.PawaToClockIn || pawa > 100) return new ShiftStart { Error = ShiftError.PawaLow };
            if (Open() != null) return new ShiftStart { Error = ShiftError.Busy };
            if (PaidToday(now) >= ShiftRules.PaidShiftsPerDay) return new ShiftStart { Error = ShiftError.CapReached };
            var sh = new Shift
            {
                Id = "local-" + (++_nextId), Job = job, Slot = slot, ClientRef = clientRef,
                Started = now, LastTrip = now, WatDay = ShiftRules.WatDay(now),
            };
            _shifts.Add(sh);
            return new ShiftStart { Ok = true, ShiftId = sh.Id, ShiftsToday = PaidToday(now), EndsByUtc = now.AddMinutes(ShiftRules.ShiftMinutes) };
        }

        public TripResult RecordTrip(string shiftId, int seats, int faresRight, int faresTotal, string clientRef)
        {
            DateTime now = _nowUtc();
            LazyClose(now);
            Shift s = Find(shiftId);
            if (s == null || s.Status != "open") return new TripResult { Error = ShiftError.NotOpen };
            if (!string.IsNullOrEmpty(clientRef) && s.TripsByRef.TryGetValue(clientRef, out TripResult again)) return again;
            if (string.IsNullOrEmpty(clientRef) || seats < 0 || seats > ShiftRules.Seats ||
                faresTotal != ShiftRules.FaresPerTrip || faresRight < 0 || faresRight > faresTotal)
                return new TripResult { Error = ShiftError.BadInput };
            if (s.Trips >= ShiftRules.MaxTripsPerShift) return new TripResult { Error = ShiftError.TooMany };
            if ((now - s.LastTrip).TotalSeconds < ShiftRules.MinSecondsBetweenTrips) return new TripResult { Error = ShiftError.TooFast };
            if ((now - s.Started).TotalMinutes > ShiftRules.ShiftHardStopMinutes) return new TripResult { Error = ShiftError.NotOpen };

            long pay = ShiftRules.TripPay(seats, faresRight, Level);
            s.Trips++;
            s.LastTrip = now;
            s.Pending += pay;
            s.TripSeats.Add(seats);
            var r = new TripResult { Ok = true, TripNo = s.Trips, TripPay = pay, ShiftPending = s.Pending };
            s.TripsByRef[clientRef] = r;
            return r;
        }

        public ShiftEnd End(string shiftId, string reason)
        {
            DateTime now = _nowUtc();
            LazyClose(now);
            Shift s = Find(shiftId);
            if (s == null) return new ShiftEnd { Error = ShiftError.NotOpen };
            if (s.Closed.HasValue) return s.Closed.Value;   // idempotent
            return Close(s, now);
        }

        /// <summary>Close any open shift older than 20 minutes (the server does this at the top of every RPC).</summary>
        void LazyClose(DateTime now)
        {
            Shift s = Open();
            if (s != null && (now - s.Started).TotalMinutes > ShiftRules.StaleShiftMinutes)
                Close(s, s.LastTrip > s.Started ? s.LastTrip : now);
        }

        ShiftEnd Close(Shift s, DateTime endedAt)
        {
            s.Ended = endedAt;
            ShiftEnd result;
            if (ShiftRules.IsPayable(s.Started, endedAt, s.Trips))
            {
                int levelBefore = Level;
                long paid = Math.Min(s.Pending, ShiftRules.ShiftCeiling(levelBefore));
                s.Status = "paid";
                s.Paid = paid;
                foreach (int seats in s.TripSeats) if (seats >= ShiftRules.MinSeatsToDepart) _countedTrips++;
                Balance += paid;   // the one ledger row: source job_shift, client_ref "shift:<id>"
                result = new ShiftEnd { Ok = true, Paid = paid, Voided = false, BalanceAfter = Balance };
            }
            else
            {
                s.Status = "void";
                result = new ShiftEnd { Ok = true, Paid = 0, Voided = true, BalanceAfter = Balance };
            }
            s.Closed = result;
            return result;
        }

        int PaidToday(DateTime now)
        {
            DateTime day = ShiftRules.WatDay(now);
            int n = 0;
            foreach (Shift s in _shifts) if (s.Status == "paid" && s.WatDay == day) n++;
            return n;
        }

        Shift Open() { foreach (Shift s in _shifts) if (s.Status == "open") return s; return null; }
        Shift Find(string id) { foreach (Shift s in _shifts) if (s.Id == id) return s; return null; }
    }
}
