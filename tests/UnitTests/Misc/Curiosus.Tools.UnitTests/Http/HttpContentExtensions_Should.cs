#nullable enable

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Curiosus.Tools.UnitTests.Http
{
    public class HttpContentExtensions_Should
    {
        private const string Text = "Привет, мир";

        private static HttpContent CreateContent(byte[] bytes, string? contentType)
        {
            var content = new ByteArrayContent(bytes);
            if (contentType != null)
                content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            return content;
        }

        [Theory]
        [InlineData("application/json; charset=windows-1251")]
        [InlineData("application/json; charset=\"windows-1251\"")]
        public async Task ReadAsStringOrUtf8Async_CodePageCharset_Decodes(string contentType)
        {
            // arrange
            var bytes = CodePagesEncodingProvider.Instance.GetEncoding(1251)!.GetBytes(Text);

            // act
            var result = await CreateContent(bytes, contentType).ReadAsStringOrUtf8Async();

            // assert
            result.Should().Be(Text);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("text/plain")]
        [InlineData("text/plain; charset=utf-8")]
        [InlineData("text/plain; charset=no-such-charset")]
        public async Task ReadAsStringOrUtf8Async_NoOrUnknownCharset_DecodesUtf8(string? contentType)
        {
            // act
            var result = await CreateContent(Encoding.UTF8.GetBytes(Text), contentType).ReadAsStringOrUtf8Async();

            // assert
            result.Should().Be(Text);
        }

        [Fact]
        public async Task ReadAsStringOrUtf8Async_Utf8Bom_SkipsBom()
        {
            // arrange
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(Text));

            // act
            var result = await CreateContent(bytes, "application/json; charset=windows-1251").ReadAsStringOrUtf8Async();

            // assert
            result.Should().Be(Text);
        }

        [Fact]
        public void IsCommunicationFailure_ClassifiesExceptions()
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            HttpFailure.IsCommunicationFailure(new HttpRequestException(), CancellationToken.None).Should().BeTrue();
            HttpFailure.IsCommunicationFailure(new TaskCanceledException(), CancellationToken.None).Should().BeTrue();
            HttpFailure.IsCommunicationFailure(new TaskCanceledException(), cancelled.Token).Should().BeFalse();
            HttpFailure.IsCommunicationFailure(new InvalidOperationException(), CancellationToken.None).Should().BeFalse();
        }
    }

    internal static class ByteArrayExtensions
    {
        public static byte[] Concat(this byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            return result;
        }
    }
}
