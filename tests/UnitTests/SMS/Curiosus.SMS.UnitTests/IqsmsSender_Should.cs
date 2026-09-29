#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Curiosus.SMS.Iqsms;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Curiosus.SMS.UnitTests
{
    public class IqsmsSender_Should
    {
        private readonly FakeHttpMessageHandler _handler = new();
        private readonly FakeHttpClientFactory _httpClientFactory;

        public IqsmsSender_Should()
        {
            _httpClientFactory = new FakeHttpClientFactory(_handler);
        }

        private IqsmsSender CreateSender(string? senderName = "SIIS") =>
            new(
                NullLogger<IqsmsSender>.Instance,
                new IqsmsOptions { Login = "login", Password = "secret", Sender = senderName },
                _httpClientFactory);

        [Fact]
        public async Task SendSmsAsync_GetsWithQueryParametersAndNormalizedPhone()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "accepted;A1", "text/plain");

            // act
            await CreateSender().SendSmsAsync("89001234567", "Привет & пока");

            // assert
            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Method.Should().Be(HttpMethod.Get);
            request.Uri.GetLeftPart(System.UriPartial.Path).Should().Be("https://api.iqsms.ru/messages/v2/send");
            var query = HttpUtility.ParseQueryString(request.Uri.Query);
            query["login"].Should().Be("login");
            query["password"].Should().Be("secret");
            query["phone"].Should().Be("+79001234567");
            query["text"].Should().Be("Привет & пока");
            query["sender"].Should().Be("SIIS");
            _httpClientFactory.CreatedClientNames.Should().OnlyContain(n => n == IqsmsSender.HttpClientName);
        }

        [Fact]
        public async Task SendSmsAsync_WithoutSenderName_DoesNotSendSenderParameter()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "accepted;A1", "text/plain");

            // act
            await CreateSender(senderName: null).SendSmsAsync("+79001234567", "text");

            // assert
            var query = HttpUtility.ParseQueryString(_handler.Requests.Should().ContainSingle().Subject.Uri.Query);
            query["sender"].Should().BeNull();
        }

        [Fact]
        public async Task SendSmsAsync_Accepted_ReturnsSuccessWithServerResponse()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "accepted;A1", "text/plain");

            // act
            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            // assert
            response.IsSuccess.Should().BeTrue();
            response.Body.ResponseJson.Should().Be("accepted;A1");
        }

        [Theory]
        [InlineData("error;not enough balance", SmsError.NoMoney)]
        [InlineData("error;invalid mobile phone", SmsError.DeliveryError)]
        [InlineData("error;something else", SmsError.Unknown)]
        public async Task SendSmsAsync_Rejected_MapsError(string content, SmsError expectedError)
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, content, "text/plain");

            // act
            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            // assert
            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)expectedError);
            response.Body.ResponseJson.Should().Be(content);
        }

        [Fact]
        public async Task SendSmsAsync_Unauthorized_ReturnsAuthError()
        {
            // arrange
            _handler.Respond(HttpStatusCode.Unauthorized, "bad credentials", "text/plain");

            // act
            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            // assert
            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)SmsError.Auth);
            response.Body.ResponseJson.Should().Be("bad credentials");
        }

        [Fact]
        public async Task SendSmsAsync_HttpError_ReturnsCommunicationError()
        {
            // arrange
            _handler.Respond(HttpStatusCode.BadGateway, "gateway", "text/plain");

            // act
            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            // assert
            response.IsSuccess.Should().BeFalse();
            var error = response.Errors.Should().ContainSingle().Subject;
            error.Code.Should().Be((int)SmsError.Communication);
            error.Description.Should().Contain("502");
            response.Body.ResponseJson.Should().Be("gateway");
        }

        [Fact]
        public async Task SendSmsAsync_NetworkFailure_ReturnsCommunicationError()
        {
            // arrange
            _handler.Throw(new HttpRequestException("connection refused"));

            // act
            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            // assert
            response.IsSuccess.Should().BeFalse();
            var error = response.Errors.Should().ContainSingle().Subject;
            error.Code.Should().Be((int)SmsError.Communication);
            error.Description.Should().Be("connection refused");
            response.Body.ResponseJson.Should().Be("empty content");
        }
    }
}
