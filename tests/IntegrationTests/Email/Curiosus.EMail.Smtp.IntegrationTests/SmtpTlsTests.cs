using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Curiosus.EMail.Smtp.IntegrationTests
{
    /// <summary>
    /// Mailpit offers STARTTLS with a self-signed certificate for localhost, like a relay with a certificate the host doesn't trust.
    /// </summary>
    public sealed class SmtpTlsTests : IAsyncLifetime
    {
        private const int SmtpPort = 1025;
        private const int ApiPort = 8025;

        private readonly IContainer _mailpit = new ContainerBuilder()
            .WithImage("axllent/mailpit:v1.27")
            .WithEnvironment("MP_SMTP_TLS_CERT", "sans:localhost")
            .WithEnvironment("MP_SMTP_TLS_KEY", "sans:localhost")
            .WithEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "1")
            .WithPortBinding(SmtpPort, true)
            .WithPortBinding(ApiPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("accessible via"))
            .Build();

        public Task InitializeAsync() => _mailpit.StartAsync();

        public Task DisposeAsync() => _mailpit.DisposeAsync().AsTask();

        private SmtpEMailSender CreateSender(bool allowInvalidCertificate) =>
            new(new SmtpEMailOptions
            {
                SmtpServer = "localhost",
                SmtpPort = _mailpit.GetMappedPublicPort(SmtpPort),
                SmtpLogin = "login",
                SmtpPassword = "password",
                EMailFrom = "noreply@example.com",
                SenderName = "Tests",
                AllowInvalidServerCertificate = allowInvalidCertificate,
            }, NullLogger<SmtpEMailSender>.Instance);

        [Fact]
        public async Task UntrustedCertificate_FailsByDefault()
        {
            var response = await CreateSender(allowInvalidCertificate: false)
                .SendAsync("user@example.com", "subject", "body");

            response.IsSuccess.Should().BeFalse();
            response.Errors.Should().ContainSingle().Which.Description.Should().ContainEquivalentOf("certificate");
            (await CountMessagesAsync()).Should().Be(0);
        }

        [Fact]
        public async Task UntrustedCertificate_IsAcceptedWithTheOptIn()
        {
            // the opt-in fails validation in Production, which is the default without DOTNET_ENVIRONMENT
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
            try
            {
                await SendWithOptInAsync();
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);
            }
        }

        private async Task SendWithOptInAsync()
        {
            var response = await CreateSender(allowInvalidCertificate: true)
                .SendAsync("user@example.com", "subject", "body");

            response.IsSuccess.Should().BeTrue(response.Errors.Count > 0 ? response.Errors[0].Description : null);
            (await CountMessagesAsync()).Should().Be(1);
        }

        private async Task<int> CountMessagesAsync()
        {
            using var http = new HttpClient();
            var json = await http.GetStringAsync(
                $"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(ApiPort)}/api/v1/messages");
            return JsonDocument.Parse(json).RootElement.GetProperty("total").GetInt32();
        }
    }
}
