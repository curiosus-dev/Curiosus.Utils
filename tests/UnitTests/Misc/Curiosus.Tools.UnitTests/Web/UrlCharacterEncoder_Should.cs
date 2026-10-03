#nullable enable

using Curiosus.Tools.Web.Controllers;
using FluentAssertions;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web
{
    public class UrlCharacterEncoder_Should
    {
        [Theory]
        [InlineData("https://example.com/a/b?x=1&y=2#top", "https://example.com/a/b?x=1&y=2#top")]
        [InlineData("/search?q=привет", "/search?q=%D0%BF%D1%80%D0%B8%D0%B2%D0%B5%D1%82")]
        [InlineData("/path with space", "/path%20with%20space")]
        [InlineData("/already%20encoded%D0%BF", "/already%20encoded%D0%BF")]
        [InlineData("/100%", "/100%25")]
        [InlineData("/50%off", "/50%25off")]
        [InlineData("/a\"b<c>", "/a%22b%3Cc%3E")]
        [InlineData("/emoji😀", "/emoji%F0%9F%98%80")]
        [InlineData("/keep-._~!$&'()*+,;=:@[]", "/keep-._~!$&'()*+,;=:@[]")]
        [InlineData("", "")]
        public void EncodeIllegalCharacters_EncodesOnlyCharactersNotAllowedInUrl(string url, string expected)
        {
            UrlCharacterEncoder.EncodeIllegalCharacters(url).Should().Be(expected);
        }
    }
}
