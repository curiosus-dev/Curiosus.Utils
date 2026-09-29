#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Curiosus.EMail;
using Curiosus.EMail.Mailgun;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Curiosus.Emails.UnitTests
{
    public class MailgunEmailSender_Should
    {
        private readonly FakeHttpMessageHandler _handler = new();
        private readonly FakeHttpClientFactory _httpClientFactory;

        public MailgunEmailSender_Should()
        {
            _httpClientFactory = new FakeHttpClientFactory(_handler);
        }

        private MailgunEmailSender CreateSender(MailgunRegion region = MailgunRegion.US, string? replyTo = null) =>
            new(
                NullLogger<MailgunEmailSender>.Instance,
                new MailgunEmailOptions
                {
                    MailgunRegion = region,
                    MailgunUser = "api",
                    MailgunApiKey = "key-123",
                    MailgunDomain = "mg.example.com",
                    EmailFrom = "noreply@example.com",
                    ReplyTo = replyTo
                },
                _httpClientFactory);

        [Fact]
        public async Task SendAsync_PostsFormWithBasicAuth()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "{\"message\":\"Queued\",\"id\":\"<1@mg>\"}");

            // act
            await CreateSender(replyTo: "support@example.com")
                .SendAsync("john@example.com", "Тема & co", "<p>Привет</p>", isBodyHtml: true);

            // assert
            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Method.Should().Be(HttpMethod.Post);
            request.Uri.Should().Be(new Uri("https://api.mailgun.net/v3/mg.example.com/messages"));
            request.Headers["Authorization"].Should()
                .Be("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("api:key-123")));
            request.ContentType.Should().Be("application/x-www-form-urlencoded");
            var form = HttpUtility.ParseQueryString(request.Body!);
            form["from"].Should().Be("noreply@example.com");
            form["to"].Should().Be("john@example.com");
            form["h:Reply-To"].Should().Be("support@example.com");
            form["subject"].Should().Be("Тема & co");
            form["html"].Should().Be("<p>Привет</p>");
            form["text"].Should().BeNull();
            _httpClientFactory.CreatedClientNames.Should().OnlyContain(n => n == MailgunEmailSender.HttpClientName);
        }

        [Fact]
        public async Task SendAsync_PlainTextInEuRegion_UsesEuHostAndTextField()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "{\"message\":\"Queued\",\"id\":\"<1@mg>\"}");

            // act
            await CreateSender(MailgunRegion.EU).SendAsync("john@example.com", "Subject", "Hello");

            // assert
            var request = _handler.Requests.Should().ContainSingle().Subject;
            request.Uri.Should().Be(new Uri("https://api.eu.mailgun.net/v3/mg.example.com/messages"));
            var form = HttpUtility.ParseQueryString(request.Body!);
            form["text"].Should().Be("Hello");
            form["html"].Should().BeNull();
            form["h:Reply-To"].Should().BeNull();
        }

        [Fact]
        public async Task SendAsync_JsonResponse_ReturnsSuccess()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "{\"message\":\"Queued\",\"id\":\"<1@mg>\"}");

            // act
            var response = await CreateSender().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task SendAsync_InvalidJsonResponse_ReturnsCommunicationError()
        {
            // arrange
            _handler.Respond(HttpStatusCode.OK, "not json");

            // act
            var response = await CreateSender().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Code.Should().Be((int)EmailError.Communication);
        }

        [Theory]
        [InlineData(420, EmailError.RateLimit)]
        [InlineData(401, EmailError.Auth)]
        [InlineData(400, EmailError.Auth)]
        public async Task SendAsync_HttpError_MapsStatusCode(int statusCode, EmailError expectedError)
        {
            // arrange
            _handler.Respond((HttpStatusCode)statusCode, "Forbidden", "text/plain");

            // act
            var response = await CreateSender().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.IsSuccess.Should().BeFalse();
            var error = response.Errors.Should().ContainSingle().Subject;
            error.Code.Should().Be((int)expectedError);
            error.Description.Should().Be("Forbidden");
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
    }
}
