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

        /// <summary>
        /// Flip to true in the same commit that records migration 0042 as applied
        /// (Ted's team). Until then shifts use the Editor stub even when signed in.
        /// </summary>
        public const bool ServerShiftsLive = false;

        public static IShiftService Current
        {
            get
            {
                if (_current != null) return _current;
                // One shared Supabase session (NetBootstrap): a second session would sign in
                // anonymously again and make a second account.
                if (ServerShiftsLive && NetBootstrap.Supabase != null && NetBootstrap.SupabaseCfg != null)
                    UseServer(NetBootstrap.SupabaseCfg, NetBootstrap.Supabase);
                else _current = CreateStub();
                return _current;
            }
        }

        /// <summary>Switch to the server. Pass NetBootstrap.SupabaseCfg and NetBootstrap.Supabase, never a new session.</summary>
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
