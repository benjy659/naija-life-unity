// Naija Streets (Unity) - IShiftService over the in-memory ShiftLedger.
// Editor and development builds only (spec 8.3): the Unity factory refuses it
// in release builds. Nothing is saved, nothing syncs, the wallet shows TEST.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NaijaLife.Jobs
{
    public sealed class LocalShiftService : IShiftService
    {
        public ShiftLedger Ledger { get; }
        public bool IsTestStub => true;

        public LocalShiftService(Func<DateTime> nowUtc = null, long startBalance = 8000)
        {
            // ₦8,000 "waka money" starter, as the balance sheet sets for a new player.
            Ledger = new ShiftLedger(nowUtc, startBalance);
        }

        public Task<ShiftStart> StartShiftAsync(JobKind job, string slotId, int pawa, string clientRef, CancellationToken cancel) =>
            Task.FromResult(Ledger.Start(JobKindNames.Id(job), slotId, pawa, clientRef));

        public Task<TripResult> RecordTripAsync(string shiftId, int seats, int faresRight, int faresTotal, string clientRef, CancellationToken cancel) =>
            Task.FromResult(Ledger.RecordTrip(shiftId, seats, faresRight, faresTotal, clientRef));

        public Task<ShiftEnd> EndShiftAsync(string shiftId, string reason, CancellationToken cancel) =>
            Task.FromResult(Ledger.End(shiftId, reason));
    }
}
