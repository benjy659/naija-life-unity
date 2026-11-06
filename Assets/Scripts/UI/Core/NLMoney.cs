// Naija Streets: ONE money format for the whole game.
// Spec: docs/specs/hud-and-phone.md section 8.4 (ported from the browser
// game's nl-money.js, owner approved 2026-10-16: "1.2bn", never "1000m").
//
//   Short(950)           -> "₦950"
//   Short(12500)         -> "₦12.5k"
//   Short(125000)        -> "₦125k"
//   Short(999960)        -> "₦1m"     (unit picked after rounding)
//   Short(1234000000)    -> "₦1.2bn"
//   Short(-4500)         -> "-₦4.5k"
//   Full(1234567)        -> "₦1,234,567"
//
// Whole naira only (the ledger stores whole naira as long).

using System;
using System.Globalization;

namespace NaijaLife.UI
{
    public static class NLMoney
    {
        public const string Sign = "₦"; // ₦

        private static readonly long[] UnitValues = { 1_000_000_000_000L, 1_000_000_000L, 1_000_000L, 1_000L };
        private static readonly string[] UnitNames = { "tn", "bn", "m", "k" };

        /// <summary>Short HUD format: ₦12.5k, ₦1.2bn.</summary>
        public static string Short(long amount, bool withSign = true)
        {
            string sign = withSign ? Sign : "";
            string neg = amount < 0 ? "-" : "";
            // long.MinValue cannot be negated; clamp (never reached by real balances)
            ulong n = amount == long.MinValue ? (ulong)long.MaxValue + 1UL : (ulong)Math.Abs(amount);
            if (n < 1000UL) return neg + sign + n.ToString(CultureInfo.InvariantCulture);

            int i = 0; // largest unit first
            while (i < UnitValues.Length - 1 && n < (ulong)UnitValues[i]) i++;
            double r = Round((double)n / UnitValues[i]);
            if (r >= 1000d && i > 0) { i--; r = Round((double)n / UnitValues[i]); }
            return neg + sign + r.ToString("0.#", CultureInfo.InvariantCulture) + UnitNames[i];
        }

        /// <summary>Full Bank format: ₦1,234,567.</summary>
        public static string Full(long amount, bool withSign = true)
        {
            string sign = withSign ? Sign : "";
            string neg = amount < 0 ? "-" : "";
            ulong n = amount == long.MinValue ? (ulong)long.MaxValue + 1UL : (ulong)Math.Abs(amount);
            return neg + sign + n.ToString("#,0", CultureInfo.InvariantCulture);
        }

        /// <summary>Signed change for history rows and toasts: +₦1,200 / -₦300.</summary>
        public static string Delta(long amount)
        {
            return amount > 0 ? "+" + Full(amount) : Full(amount);
        }

        // One decimal below 100 of a unit, none from 100 up (12.46 -> 12.5, 125.4 -> 125).
        private static double Round(double x)
        {
            return x >= 100d
                ? Math.Round(x, MidpointRounding.AwayFromZero)
                : Math.Round(x * 10d, MidpointRounding.AwayFromZero) / 10d;
        }
    }
}
