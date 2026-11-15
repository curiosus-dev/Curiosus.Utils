#nullable enable

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.SMS.Iqsms;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Curiosus.SMS.UnitTests
{
    public class IqsmsSender_Should
    {
        private const string Accepted =
            "{\"status\":\"ok\",\"messages\":[{\"clientId\":\"1\",\"smscId\":\"12345\",\"status\":\"accepted\"}]}";

        private readonly FakeHttpMessageHandler _handler = new();
        private readonly FakeHttpClientFactory _httpClientFactory;
        private readonly FakeLogger<IqsmsSender> _logger = new();

        public IqsmsSender_Should()
        {
            _httpClientFactory = new FakeHttpClientFactory(_handler);
        }

        private IqsmsSender CreateSender(string? senderName = "SIIS") =>
            new(
                _logger,
                new IqsmsOptions { Login = "login", Password = "secret", Sender = senderName },
                _httpClientFactory);

        private static string MessageStatus(string status) =>
            "{\"status\":\"ok\",\"messages\":[{\"clientId\":\"1\",\"smscId\":\"1\",\"status\":\"" + status + "\"}]}";

        [Fact]
        public async Task SendSmsAsync_PostsJsonWithCredentialsInTheBody_NothingInTheUrl()
        {
            _handler.Respond(HttpStatusCode.OK, Accepted);

            await CreateSender().SendSmsAsync("89001234567", "Привет & пока");

            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Method.Should().Be(HttpMethod.Post);
            request.Uri.ToString().Should().Be("https://api.iqsms.ru/messages/v2/send.json");
            request.Uri.Query.Should().BeEmpty();
            request.ContentType.Should().StartWith("application/json");

            using var body = JsonDocument.Parse(request.Body!);
            body.RootElement.GetProperty("login").GetString().Should().Be("login");
            body.RootElement.GetProperty("password").GetString().Should().Be("secret");
            var message = body.RootElement.GetProperty("messages").EnumerateArray().Should().ContainSingle().Subject;
            message.GetProperty("phone").GetString().Should().Be("+79001234567");
            message.GetProperty("text").GetString().Should().Be("Привет & пока");
            message.GetProperty("sender").GetString().Should().Be("SIIS");
            message.GetProperty("clientId").GetString().Should().Be("1");
            _httpClientFactory.CreatedClientNames.Should().OnlyContain(n => n == IqsmsSender.HttpClientName);
        }

        [Fact]
        public async Task SendSmsAsync_WithoutSenderName_OmitsSender()
        {
            _handler.Respond(HttpStatusCode.OK, Accepted);

            await CreateSender(senderName: null).SendSmsAsync("+79001234567", "text");

            using var body = JsonDocument.Parse(_handler.Requests.Single().Body!);
            body.RootElement.GetProperty("messages")[0].TryGetProperty("sender", out _).Should().BeFalse();
        }

        [Fact]
        public async Task SendSmsAsync_Accepted_ReturnsSuccessWithRawJson()
        {
            _handler.Respond(HttpStatusCode.OK, Accepted);

            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            response.IsSuccess.Should().BeTrue();
            response.Body.ResponseJson.Should().Be(Accepted);
        }

        [Theory]
        [InlineData("not enough balance", SmsError.NoMoney)]
        [InlineData("not enough credits", SmsError.NoMoney)]
        [InlineData("invalid mobile phone", SmsError.DeliveryError)]
        [InlineData("sender address invalid", SmsError.Unknown)]
        public async Task SendSmsAsync_RejectedMessage_MapsError(string status, SmsError expected)
        {
            var content = MessageStatus(status);
            _handler.Respond(HttpStatusCode.OK, content);

            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)expected);
            response.Body.ResponseJson.Should().Be(content);
        }

        [Theory]
        [InlineData("{\"status\":\"error\",\"description\":\"error authorization\"}", SmsError.Auth)]
        [InlineData("{\"status\":\"error\",\"description\":\"something else\"}", SmsError.Unknown)]
        [InlineData("not json", SmsError.Unknown)]
        public async Task SendSmsAsync_PackageError_MapsError(string content, SmsError expected)
        {
            _handler.Respond(HttpStatusCode.OK, content);

            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)expected);
        }

        [Fact]
        public async Task SendSmsAsync_Unauthorized_ReturnsAuthError()
        {
            _handler.Respond(HttpStatusCode.Unauthorized, "bad credentials", "text/plain");

            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)SmsError.Auth);
            response.Body.ResponseJson.Should().Be("bad credentials");
        }

        [Fact]
        public async Task SendSmsAsync_HttpError_ReturnsCommunicationError()
        {
            _handler.Respond(HttpStatusCode.BadGateway, "gateway", "text/plain");

            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            var error = response.Errors.Should().ContainSingle().Subject;
            error.Code.Should().Be((int)SmsError.Communication);
            error.Description.Should().Contain("502");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SendSmsAsync_NetworkFailureOrTimeout_ReturnsCommunicationError(bool timeout)
        {
            _handler.Throw(timeout
                ? new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")
                : new HttpRequestException("connection refused"));

            var response = await CreateSender().SendSmsAsync("+79001234567", "text");

            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)SmsError.Communication);
        }

        [Fact]
        public async Task SendSmsAsync_CancelledByCaller_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var act = () => CreateSender().SendSmsAsync("79001234567", "text", cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Theory]
        [InlineData(Accepted)]
        [InlineData("{\"status\":\"ok\",\"messages\":[{\"clientId\":\"1\",\"smscId\":\"1\",\"status\":\"invalid mobile phone\"}]}")]
        public async Task Logs_ContainNoCredentialsAndOnlyAMaskedPhone(string content)
        {
            _handler.Respond(HttpStatusCode.OK, content);

            await CreateSender().SendSmsAsync("+79001234567", "text");

            var records = _logger.Collector.GetSnapshot().Where(r => r.Level >= LogLevel.Information).ToList();
            records.Should().NotBeEmpty();
            records.Should().OnlyContain(r =>
                !r.Message.Contains("secret") && !r.Message.Contains("login") && !r.Message.Contains("+79001234567"));
            records.Should().Contain(r => r.Message.Contains("+7******4567"));
        }
    }
}
