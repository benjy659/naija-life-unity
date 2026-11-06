// Naija Streets (Unity rebuild) — NLMoney EditMode tests.
// Spec: docs/specs/hud-and-phone.md section 8.4. Every example in the spec
// is covered here, plus the rounding edge cases.

using NUnit.Framework;
using NaijaLife.UI;

namespace NaijaLife.UI.Tests
{
    public class NLMoneyTests
    {
        [TestCase(950L, "₦950")]
        [TestCase(999L, "₦999")]
        [TestCase(1000L, "₦1k")]
        [TestCase(10500L, "₦10.5k")]
        [TestCase(12500L, "₦12.5k")]
        [TestCase(125000L, "₦125k")]
        [TestCase(100000L, "₦100k")]
        [TestCase(999499L, "₦999k")]
        [TestCase(999960L, "₦1m")]
        [TestCase(999999L, "₦1m")]
        [TestCase(850000000L, "₦850m")]
        [TestCase(1234000000L, "₦1.2bn")]
        [TestCase(2000000000000L, "₦2tn")]
        [TestCase(-4500L, "-₦4.5k")]
        [TestCase(-125000L, "-₦125k")]
        [TestCase(0L, "₦0")]
        public void Short_FormatsPerSpec(long n, string expected)
        {
            Assert.AreEqual(expected, NLMoney.Short(n));
        }

        [TestCase(8450L, "₦8,450")]
        [TestCase(1234567L, "₦1,234,567")]
        [TestCase(950L, "₦950")]
        [TestCase(-1200L, "-₦1,200")]
        [TestCase(0L, "₦0")]
        public void Full_FormatsPerSpec(long n, string expected)
        {
            Assert.AreEqual(expected, NLMoney.Full(n));
        }

        [Test]
        public void ShortSigned_AddsExplicitSign()
        {
            Assert.AreEqual("+₦1,200", NLMoney.ShortSigned(1200, true));
            Assert.AreEqual("-₦1,200", NLMoney.ShortSigned(-1200, false));
            Assert.AreEqual("-₦300", NLMoney.ShortSigned(300, false));
        }
    }
}
