#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Curiosus.Tools.Web.ReCaptcha;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web
{
    public class ReCaptchaService_Should
    {
        private readonly FakeHttpMessageHandler _handler = new();
        private readonly FakeHttpClientFactory _httpClientFactory;

        public ReCaptchaService_Should()
        {
            _httpClientFactory = new FakeHttpClientFactory(_handler);
        }

        private ReCaptchaService CreateService() =>
            new(
                new ReCaptchaOptions
                {
                    ReCaptchaApiUrl = "https://www.google.com/recaptcha/api/siteverify",
                    ReCaptchaClientKey = "client",
                    ReCaptchaServerKey = "server & key"
                },
                _httpClientFactory);

        [Fact]
        public async Task VerifyReCaptchaAsync_SendsSecretAndResponseInQuery()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "{\"success\":true,\"hostname\":\"example.com\"}");

            // act
            await CreateService().VerifyReCaptchaAsync("token+/=");

            // assert
            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Method.Should().Be(HttpMethod.Get);
            request.Uri.GetLeftPart(System.UriPartial.Path).Should().Be("https://www.google.com/recaptcha/api/siteverify");
            var query = HttpUtility.ParseQueryString(request.Uri.Query);
            query["secret"].Should().Be("server & key");
            query["response"].Should().Be("token+/=");
            _httpClientFactory.CreatedClientNames.Should().OnlyContain(n => n == ReCaptchaService.HttpClientName);
        }

        [Theory]
        [InlineData("{\"success\":true}", true)]
        [InlineData("{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}", false)]
        public async Task VerifyReCaptchaAsync_ReturnsSuccessFlag(string content, bool expected)
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, content);

            // act
            var result = await CreateService().VerifyReCaptchaAsync("token");

            // assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        public async Task VerifyReCaptchaAsync_EmptyResponse_ReturnsFalseWithoutRequest(string? response)
        {
            // act
            var result = await CreateService().VerifyReCaptchaAsync(response!);

            // assert
            result.Should().BeFalse();
            _handler.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task VerifyReCaptchaAsync_HttpError_Throws()
        {
            // arrange
            _handler.Respond(HttpStatusCode.InternalServerError, "oops", "text/plain");

            // act
            var act = () => CreateService().VerifyReCaptchaAsync("token");

            // assert
            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }
}
