using FluentAssertions;
using Xunit;

namespace Curiosus.SMS.UnitTests
{
    public class PhoneNumberMask_Should
    {
        [Theory]
        [InlineData("+79001234567", "+7******4567")]
        [InlineData("89001234567", "89*****4567")]
        [InlineData("12345", "***45")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Mask_KeepsOnlyTheEdges(string? phone, string expected)
        {
            PhoneNumberMask.Mask(phone).Should().Be(expected);
        }
    }
}
