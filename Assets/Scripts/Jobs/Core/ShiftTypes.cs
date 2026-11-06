// Naija Streets (Unity) - M4 shift service contract (docs/specs/jobs-m4.md section 8).
// Gameplay talks only to IShiftService. Two implementations: LocalShiftService
// (Editor and development builds, in memory) and SupabaseShiftService (the
// nl_shift_* RPCs). The client never decides pay and never adds to the wallet.

using System.Threading;
using System.Threading.Tasks;

namespace NaijaLife.Jobs
{
    public enum JobKind { Conductor = 0 }

    public static class JobKindNames
    {
        /// <summary>The server's job id. Never rename once live.</summary>
        public static string Id(JobKind k) => k == JobKind.Conductor ? "conductor" : "unknown";
    }

    /// <summary>Error codes, same strings as the server.</summary>
    public static class ShiftError
    {
        public const string PawaLow = "pawa_low";
        public const string CapReached = "cap_reached";
        public const string Busy = "busy";
        public const string TooFast = "too_fast";
        public const string TooMany = "too_many";
        public const string NotOpen = "not_open";
        public const string BadInput = "bad_input";
        public const string Network = "network";   // client side only

        /// <summary>What the player sees (spec 8.2). Null means show nothing.</summary>
        public static string PlayerText(string code)
        {
            switch (code)
            {
                case PawaLow: return "You too tire. Chop first.";
                case CapReached: return "You don work 3 shifts today. Come back tomorrow.";
                case Busy: return "Finish your other shift first.";
                case TooMany: return "Shift full. Clock out to get paid.";
                case Network: return "We go save am when network come back.";
                default: return null;
            }
        }
    }

    public struct ShiftStart
    {
        public bool Ok; public string Error;
        public string ShiftId; public int ShiftsToday; public System.DateTime EndsByUtc;
    }

    public struct TripResult
    {
        public bool Ok; public string Error;
        public int TripNo; public long TripPay; public long ShiftPending;
    }

    public struct ShiftEnd
    {
        public bool Ok; public string Error;
        public long Paid; public bool Voided; public long BalanceAfter;
    }

    public interface IShiftService
    {
        /// <summary>True for the in-memory stub: the wallet shows TEST.</summary>
        bool IsTestStub { get; }
        Task<ShiftStart> StartShiftAsync(JobKind job, string slotId, int pawa, string clientRef, CancellationToken cancel);
        Task<TripResult> RecordTripAsync(string shiftId, int seats, int faresRight, int faresTotal, string clientRef, CancellationToken cancel);
        Task<ShiftEnd> EndShiftAsync(string shiftId, string reason, CancellationToken cancel);
    }
}
