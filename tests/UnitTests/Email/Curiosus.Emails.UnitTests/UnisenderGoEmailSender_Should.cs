#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Curiosus.EMail;
using Curiosus.Email.UnisenderGo;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Curiosus.Emails.UnitTests
{
    public class UnisenderGoEmailSender_Should
    {
        private const string SuccessResponse = "{\"status\":\"success\",\"job_id\":\"1ZymBc-00041N-9X\",\"emails\":[\"john@example.com\"]}";

        private readonly FakeHttpMessageHandler _handler = new();
        private readonly FakeHttpClientFactory _httpClientFactory;

        public UnisenderGoEmailSender_Should()
        {
            _httpClientFactory = new FakeHttpClientFactory(_handler);
        }

        private UnisenderGoEmailSender CreateSender() =>
            new(
                NullLogger<UnisenderGoEmailSender>.Instance,
                new UnisenderGoEmailOptions
                {
                    ApiKey = "api-key",
                    EmailFrom = "noreply@example.com",
                    FromName = "Example",
                    ReplyTo = "support@example.com",
                    TrackLinks = true
                },
                _httpClientFactory);

        [Fact]
        public async Task SendAsync_PostsJsonWithApiKeyHeader()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, SuccessResponse);

            // act
            await CreateSender().SendAsync("john@example.com", "Тема", "<p>Привет</p>", isBodyHtml: true);

            // assert
            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Method.Should().Be(HttpMethod.Post);
            request.Uri.Should().Be(new Uri("https://go1.unisender.ru/ru/transactional/api/v1/email/send.json"));
            request.Headers["X-API-KEY"].Should().Be("api-key");
            request.ContentType.Should().Be("application/json");

            var expected = new UnisenderGoSendEmailRequest
            {
                Message = new UnisenderGoSendEmailMessage
                {
                    Recipients = new[] { new UnisenderGoRecipient { Email = "john@example.com" } },
                    Body = new UnisenderGoSendEmailMessageBody { Html = "<p>Привет</p>" },
                    Subject = "Тема",
                    ReplyTo = "support@example.com",
                    FromEmail = "noreply@example.com",
                    FromName = "Example",
                    TrackLinks = 1
                }
            };
            request.Body.Should().Be(JsonSerializer.Serialize(expected, UnisenderGoEmailSender.SerializerOptions));
            _httpClientFactory.CreatedClientNames.Should().OnlyContain(n => n == UnisenderGoEmailSender.HttpClientName);
        }

        [Fact]
        public async Task SendAsync_SuccessResponse_ReturnsSuccess()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, SuccessResponse);

            // act
            var response = await CreateSender().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task SendAsync_InvalidSuccessResponse_ReturnsCommunicationError()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "not json", "text/plain");

            // act
            var response = await CreateSender().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)EmailError.Communication);
        }

        [Theory]
        [InlineData(401, "{\"status\":\"error\",\"message\":\"bad key\",\"code\":102}", EmailError.Auth)]
        [InlineData(403, "{\"status\":\"error\",\"message\":\"limit\",\"code\":901}", EmailError.RateLimit)]
        [InlineData(403, "{\"status\":\"error\",\"message\":\"inactive\",\"code\":903}", EmailError.Unknown)]
        [InlineData(403, "not json", EmailError.Auth)]
        [InlineData(400, "{\"status\":\"error\",\"message\":\"bad\",\"code\":204}", EmailError.IncorrectRequestData)]
        [InlineData(404, "", EmailError.Communication)]
        [InlineData(429, "", EmailError.RateLimit)]
        [InlineData(503, "", EmailError.Communication)]
        [InlineData(418, "", EmailError.Unknown)]
        public async Task SendAsync_HttpError_MapsStatusCode(int statusCode, string content, EmailError expectedError)
        {
            // arrange
            _handler.Respond((HttpStatusCode)statusCode, content);

            // act
            var response = await CreateSender().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)expectedError);
        }

        [Fact]
        public async Task SendAsync_ErrorResponse_IncludesUnisenderErrorInDescription()
        {
            // arrange
            _handler.Respond(HttpStatusCode.Unauthorized, "{\"status\":\"error\",\"message\":\"bad key\",\"code\":102}");

            // act
            var response = await CreateSender().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.Errors.Should().ContainSingle().Which.Description.Should().Be("UnisenderErrorCode=102: bad key");
        }

        [Fact]
        public async Task SendAsync_NetworkFailure_ReturnsCommunicationError()
        {
            // arrange
            _handler.Throw(new HttpRequestException("connection refused"));

            // act
            var response = await CreateSender().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.IsSuccess.Should().BeFalse();
            var error = response.Errors.Should().ContainSingle().Subject;
            error.Code.Should().Be((int)EmailError.Communication);
            error.Description.Should().Be("connection refused");
        }

        [Fact]
        public async Task SendAsync_CancelledByCaller_Throws()
        {
            // arrange
            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            // act
            var act = () => CreateSender().SendAsync("john@example.com", "Subject", "Hello", false, cts.Token);

            // assert
            await act.Should().ThrowAsync<System.OperationCanceledException>();
        }
    }
}
