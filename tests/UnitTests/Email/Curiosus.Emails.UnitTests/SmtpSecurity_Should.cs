using System;
using System.Linq;
using Curiosus.EMail.Smtp;
using FluentAssertions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Curiosus.Emails.UnitTests
{
    public class SmtpSecurity_Should
    {
        private static SmtpEMailOptions Options(Action<SmtpEMailOptions>? change = null)
        {
            var options = new SmtpEMailOptions
            {
                SmtpServer = "smtp.example.com",
                SmtpLogin = "login",
                SmtpPassword = "password",
                EMailFrom = "noreply@example.com",
                SenderName = "Sender",
            };
            change?.Invoke(options);
            return options;
        }

        [Theory]
        [InlineData(SecureSocketOptions.Auto)]
        [InlineData(SecureSocketOptions.StartTlsWhenAvailable)]
        public void Validation_RejectsDowngradableSecurity(SecureSocketOptions security)
        {
            Options(o => o.Security = security).Validate().Should().ContainSingle(e => e.FieldName == "Security");
        }

        [Theory]
        [InlineData(null)]
        [InlineData(SecureSocketOptions.StartTls)]
        [InlineData(SecureSocketOptions.SslOnConnect)]
        [InlineData(SecureSocketOptions.None)]
        public void Validation_AcceptsExplicitSecurity(SecureSocketOptions? security)
        {
            Options(o => o.Security = security).Validate().Should().BeEmpty();
        }

        [Fact]
        public void Validation_RejectsInvalidCertificatesWithoutTls()
        {
            Options(o =>
                {
                    o.Security = SecureSocketOptions.None;
                    o.AllowInvalidServerCertificate = true;
                })
                .Validate().Should().ContainSingle(e => e.FieldName == "AllowInvalidServerCertificate");
        }

        [Theory]
        [InlineData(25, SecureSocketOptions.StartTls)]
        [InlineData(587, SecureSocketOptions.StartTls)]
        [InlineData(465, SecureSocketOptions.SslOnConnect)]
        public void ConfigureClient_IsStrictByDefault(int port, SecureSocketOptions expected)
        {
            using var client = new SmtpClient();
            var revocation = client.CheckCertificateRevocation;

            var security = SmtpEMailSender.ConfigureClient(client, Options(o => o.SmtpPort = port));

            security.Should().Be(expected);
            client.ServerCertificateValidationCallback.Should().BeNull();
            client.CheckCertificateRevocation.Should().Be(revocation);
        }

        [Fact]
        public void ConfigureClient_UsesTheConfiguredSecurity()
        {
            using var client = new SmtpClient();

            SmtpEMailSender.ConfigureClient(client, Options(o => o.Security = SecureSocketOptions.None))
                .Should().Be(SecureSocketOptions.None);
        }

        [Fact]
        public void ConfigureClient_OptIn_AcceptsAnyCertificate()
        {
            using var client = new SmtpClient();

            SmtpEMailSender.ConfigureClient(client, Options(o => o.AllowInvalidServerCertificate = true));

            client.ServerCertificateValidationCallback.Should().NotBeNull();
            client.ServerCertificateValidationCallback!(client, null, null, System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch)
                .Should().BeTrue();
        }

        [Fact]
        public void OptIn_IsValid_InAnyEnvironment()
        {
            // the owner decides: allowed in Production too, only warned about
            Options(o => o.AllowInvalidServerCertificate = true).Validate().Should().BeEmpty();
        }

        [Fact]
        public void OptIn_LogsAWarning()
        {
            var logger = new FakeLogger<SmtpEMailSender>();

            new SmtpEMailSender(Options(o => o.AllowInvalidServerCertificate = true), logger);

            logger.Collector.GetSnapshot().Should().ContainSingle(r =>
                r.Level == LogLevel.Warning && r.Message.Contains("AllowInvalidServerCertificate"));
        }

        [Fact]
        public void DefaultOptions_LogNoWarning()
        {
            var logger = new FakeLogger<SmtpEMailSender>();

            new SmtpEMailSender(Options(), logger);

            logger.Collector.Count.Should().Be(0);
        }
    }
}
