using FluentAssertions;
using Xunit;

namespace Curiosus.Tools.UnitTests;

public sealed class PhoneHelperMaskForLogTests
{
    [Theory]
    [InlineData("+79001234567", "+7******4567")]
    [InlineData("89001234567", "89*****4567")]
    [InlineData("12345", "***45")]
    [InlineData("1", "1")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void MaskForLog_KeepsOnlyTheEdges(string? phone, string expected)
    {
        phone.MaskForLog().Should().Be(expected);
    }
}
