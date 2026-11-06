// Naija Streets (Unity rebuild) — NLMoney
// Money formatter, ported from the browser game's nl-money.js (owner-approved
// 2026-10-16). Spec: docs/specs/hud-and-phone.md section 8.4.
// Pure C#, no Unity dependencies — covered by EditMode tests (NLMoneyTests).

namespace NaijaLife.UI
{
    public static class NLMoney
    {
        /// <summary>Short format: 950 -> "₦950", 12500 -> "₦12.5k", 125000 -> "₦125k",
        /// 850000000 -> "₦850m", 1234000000 -> "₦1.2bn". One decimal below 100 of a
        /// unit, none from 100 up, trailing ".0" dropped, unit chosen AFTER rounding
        /// (999960 -> "₦1m", never "₦1000k"). Negative: "-₦4.5k".</summary>
        public static string Short(long n)
        {
            bool negative = n < 0;
            double v = System.Math.Abs((double)n);
            string sign = negative ? "-" : "";

            if (v < 1000.0)
                return sign + "₦" + ((long)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

            string[] units = { "k", "m", "bn", "tn" };
            double[] divisors = { 1000.0, 1000000.0, 1000000000.0, 1000000000000.0 };

            // Choose the unit AFTER rounding: 999,960 rounds to 1000k, so it is ₦1m.
            for (int i = 0; i < units.Length; i++)
            {
                double scaled = v / divisors[i];
                double rounded = System.Math.Round(scaled, 1, System.MidpointRounding.AwayFromZero);
                if (rounded < 1000.0 || i == units.Length - 1)
                {
                    // >= 100 of a unit: no decimal. Floor, never round up into
                    // the next thousand (999.5k is "₦999k", never "₦1000k").
                    string num = rounded >= 100.0
                        ? ((long)System.Math.Floor(rounded))
                            .ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                        : FormatOneDecimal(rounded);
                    return sign + "₦" + num + units[i];
                }
            }
            return sign + "₦" + ((long)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Full format: 1234567 -> "₦1,234,567", whole naira, comma thousands.</summary>
        public static string Full(long n)
        {
            bool negative = n < 0;
            long v = System.Math.Abs(n);
            string sign = negative ? "-" : "";
            return sign + "₦" + v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Short format with an explicit sign: money in gets "+", money out "-".
        /// e.g. ShortSigned(1200, true) -> "+₦1,200"; ShortSigned(-1200, false) -> "-₦1,200".</summary>
        public static string ShortSigned(long n, bool isIn)
        {
            return (isIn ? "+" : "-") + Short(System.Math.Abs(n));
        }

        private static string FormatOneDecimal(double v)
        {
            string s = v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            if (s.EndsWith(".0", System.StringComparison.Ordinal))
                s = s.Substring(0, s.Length - 2);
            return s;
        }
    }
}
