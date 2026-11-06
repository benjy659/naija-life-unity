// Naija Streets (Unity) - picks the shift service (docs/specs/jobs-m4.md 8.3).
// Editor and development builds may use the in-memory stub; a release build
// never does: it throws rather than show fake money.

using NaijaLife.Jobs;
using NaijaLife.NetworkGlue;

namespace NaijaLife.Economy
{
    public static class ShiftServiceFactory
    {
        static IShiftService _current;

        public static IShiftService Current => _current ?? (_current = CreateStub());

        /// <summary>Called once sign-in works (M5 bootstrap): switch to the server.</summary>
        public static void UseServer(SupabaseConfig cfg, SupabaseSession session) { _current = new SupabaseShiftService(cfg, session); }

        static IShiftService CreateStub()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            throw new System.InvalidOperationException("Release builds must call ShiftServiceFactory.UseServer before any shift. The test stub is Editor and development only.");
#else
            var stub = new LocalShiftService();
            WalletCache.AdoptServerBalance(stub.Ledger.Balance, true);
            return stub;
#endif
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _current = null; }
    }
}
