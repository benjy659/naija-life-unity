// Edit mode tests for NLMoney (spec docs/specs/hud-and-phone.md section 8.4).
using NUnit.Framework;

namespace NaijaLife.UI.Tests
{
    public class NLMoneyTests
    {
        [TestCase(0L, "₦0")]
        [TestCase(950L, "₦950")]
        [TestCase(999L, "₦999")]
        [TestCase(1000L, "₦1k")]
        [TestCase(12500L, "₦12.5k")]
        [TestCase(125000L, "₦125k")]
        [TestCase(999960L, "₦1m")]
        [TestCase(850000000L, "₦850m")]
        [TestCase(1000000000L, "₦1bn")]
        [TestCase(1234000000L, "₦1.2bn")]
        [TestCase(2500000000000L, "₦2.5tn")]
        [TestCase(-4500L, "-₦4.5k")]
        public void Short_MatchesBrowserFormat(long n, string expected)
        {
            Assert.AreEqual(expected, NLMoney.Short(n));
        }

        [Test]
        public void Short_WithoutSign()
        {
            Assert.AreEqual("12.5k", NLMoney.Short(12500, withSign: false));
        }

        [TestCase(0L, "₦0")]
        [TestCase(8450L, "₦8,450")]
        [TestCase(1234567L, "₦1,234,567")]
        [TestCase(-300L, "-₦300")]
        public void Full_CommaThousands(long n, string expected)
        {
            Assert.AreEqual(expected, NLMoney.Full(n));
        }

        [TestCase(1200L, "+₦1,200")]
        [TestCase(-300L, "-₦300")]
        [TestCase(0L, "₦0")]
        public void Delta_Signed(long n, string expected)
        {
            Assert.AreEqual(expected, NLMoney.Delta(n));
        }
    }
}
