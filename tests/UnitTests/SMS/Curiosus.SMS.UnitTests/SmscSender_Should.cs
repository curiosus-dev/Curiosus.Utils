#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Curiosus.SMS.Smsc;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Curiosus.SMS.UnitTests
{
    public class SmscSender_Should
    {
        private readonly FakeHttpMessageHandler _handler = new();
        private readonly FakeHttpClientFactory _httpClientFactory;

        public SmscSender_Should()
        {
            _httpClientFactory = new FakeHttpClientFactory(_handler);
        }

        private SmscSender CreateSender(string? senderName = "SIIS") =>
            new(
                NullLogger<SmscSender>.Instance,
                new SmscOptions { SmscLogin = "login", SmscPassword = "secret", SmscSender = senderName },
                _httpClientFactory);

        [Fact]
        public async Task SendSmsAsync_PostsQueryParametersToSmsc()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "{\"id\":1,\"cnt\":1,\"cost\":\"1.5\"}");

            // act
            await CreateSender().SendSmsAsync("79001234567", "Привет & пока");

            // assert
            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Method.Should().Be(HttpMethod.Post);
            request.Uri.GetLeftPart(System.UriPartial.Path).Should().Be("https://smsc.ru/sys/send.php");
            var query = HttpUtility.ParseQueryString(request.Uri.Query);
            query["login"].Should().Be("login");
            query["psw"].Should().Be("secret");
            query["sender"].Should().Be("SIIS");
            query["phones"].Should().Be("79001234567");
            query["mes"].Should().Be("Привет & пока");
            query["cost"].Should().Be("2");
            query["fmt"].Should().Be("3");
            _httpClientFactory.CreatedClientNames.Should().OnlyContain(n => n == SmscSender.HttpClientName);
        }

        [Fact]
        public async Task SendSmsAsync_WithoutSenderName_DoesNotSendSenderParameter()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "{\"id\":1,\"cnt\":1}");

            // act
            await CreateSender(senderName: null).SendSmsAsync("79001234567", "text");

            // assert
            var query = HttpUtility.ParseQueryString(_handler.Requests.Should().ContainSingle().Subject.Uri.Query);
            query["sender"].Should().BeNull();
        }

        [Fact]
        public async Task SendSmsAsync_SuccessResponse_ReturnsCountAndCost()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "{\"id\":123,\"cnt\":2,\"cost\":\"3.40\",\"balance\":\"100.50\"}");

            // act
            var response = await CreateSender().SendSmsAsync("79001234567", "text");

            // assert
            response.IsSuccess.Should().BeTrue();
            response.Body.SmsCount.Should().Be(2);
            response.Body.Cost.Should().Be(3.40m);
            response.Body.ResponseJson.Should().Contain("\"cnt\":2");
        }

        [Fact]
        public async Task SendSmsAsync_MessageDenied_RetriesOnceWithoutSenderName()
        {
            // arrange
            _handler
                .Respond(HttpStatusCode.OK, "{\"error\":\"message is denied\",\"error_code\":6}")
                .Respond(HttpStatusCode.OK, "{\"id\":1,\"cnt\":1}");

            // act
            var response = await CreateSender().SendSmsAsync("79001234567", "text");

            // assert
            response.IsSuccess.Should().BeTrue();
            _handler.Requests.Should().HaveCount(2);
            HttpUtility.ParseQueryString(_handler.Requests[0].Uri.Query)["sender"].Should().Be("SIIS");
            HttpUtility.ParseQueryString(_handler.Requests[1].Uri.Query)["sender"].Should().BeNull();
        }

        [Theory]
        [InlineData(2, SmsError.Auth)]
        [InlineData(3, SmsError.NoMoney)]
        [InlineData(9, SmsError.RateLimit)]
        [InlineData(8, SmsError.DeliveryError)]
        [InlineData(100, SmsError.Unknown)]
        public async Task SendSmsAsync_SmscError_MapsErrorCode(int smscErrorCode, SmsError expectedError)
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, $"{{\"error\":\"failure\",\"error_code\":{smscErrorCode}}}");

            // act
            var response = await CreateSender().SendSmsAsync("79001234567", "text");

            // assert
            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)expectedError);
            response.Body.ResponseJson.Should().Contain($"\"error_code\":{smscErrorCode}");
        }

        [Fact]
        public async Task SendSmsAsync_HttpError_ReturnsUnknownError()
        {
            // arrange
            _handler.Respond(HttpStatusCode.InternalServerError, "oops", "text/plain");

            // act
            var response = await CreateSender().SendSmsAsync("79001234567", "text");

            // assert
            response.IsSuccess.Should().BeFalse();
            var error = response.Errors.Should().ContainSingle().Subject;
            error.Code.Should().Be((int)SmsError.Unknown);
            error.Description.Should().Contain("500");
            response.Body.ResponseJson.Should().Contain("\"error_code\":-1");
        }

        [Fact]
        public async Task SendSmsAsync_NetworkFailure_ReturnsUnknownError()
        {
            // arrange
            _handler.Throw(new HttpRequestException("connection refused"));

            // act
            var response = await CreateSender().SendSmsAsync("79001234567", "text");

            // assert
            response.IsSuccess.Should().BeFalse();
            var error = response.Errors.Should().ContainSingle().Subject;
            error.Code.Should().Be((int)SmsError.Unknown);
            error.Description.Should().Be("HttpRequestException: connection refused");
        }

        [Fact]
        public async Task SendSmsAsync_InvalidJson_ReturnsUnknownError()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "not json", "text/plain");

            // act
            var response = await CreateSender().SendSmsAsync("79001234567", "text");

            // assert
            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)SmsError.Unknown);
        }
    }
}
