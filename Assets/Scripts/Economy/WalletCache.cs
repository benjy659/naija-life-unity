// Naija Streets (Unity) - the wallet the HUD and phone read
// (docs/specs/jobs-m4.md section 8.4). A read cache of the server balance:
// AdoptServerBalance is the only setter. The client never adds pay itself.

using System;

namespace NaijaLife.Economy
{
    public static class WalletCache
    {
        public static long Balance { get; private set; }
        public static bool Known { get; private set; }
        /// <summary>True while the balance comes from the Editor stub: show "TEST".</summary>
        public static bool IsTest { get; private set; }
        public static event Action<long> Changed;

        public static void AdoptServerBalance(long balanceAfter, bool fromTestStub)
        {
            Balance = balanceAfter;
            Known = true;
            IsTest = fromTestStub;
            Changed?.Invoke(balanceAfter);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Balance = 0; Known = false; IsTest = false; Changed = null; }
    }
}
