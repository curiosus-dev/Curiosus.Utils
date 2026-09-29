#nullable enable

using System.Net;
using System.Threading.Tasks;
using Curiosus.EMail;
using Curiosus.EMail.Mailgun;
using Curiosus.Email.UnisenderGo;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Xunit;

namespace Curiosus.Emails.UnitTests
{
    public class IoCExtensions_Should
    {
        [Fact]
        public async Task AddMailgunEmailSender_SendsThroughNamedHttpClient()
        {
            // arrange
            var handler = new FakeHttpMessageHandler().Respond(HttpStatusCode.OK, "{\"message\":\"Queued\",\"id\":\"1\"}");
            var services = new ServiceCollection().AddLogging();
            services.AddMailgunEmailSender(new MailgunEmailOptions
            {
                MailgunUser = "api",
                MailgunApiKey = "key",
                MailgunDomain = "mg.example.com",
                EmailFrom = "noreply@example.com"
            });
            services.AddHttpClient(MailgunEmailSender.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            await using var provider = services.BuildServiceProvider();

            // act
            var response = await provider.GetRequiredService<IEMailSender>().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.IsSuccess.Should().BeTrue();
            handler.Requests.Should().ContainSingle();
        }

        [Fact]
        public async Task AddUnisenderGoEmailSender_SendsThroughNamedHttpClient()
        {
            // arrange
            var handler = new FakeHttpMessageHandler().Respond(HttpStatusCode.OK, "{\"status\":\"success\",\"job_id\":\"1\"}");
            var services = new ServiceCollection().AddLogging();
            services.AddUnisenderGoEmailSender(new UnisenderGoEmailOptions
            {
                ApiKey = "key",
                EmailFrom = "noreply@example.com",
                FromName = "Example"
            });
            services.AddHttpClient(UnisenderGoEmailSender.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            await using var provider = services.BuildServiceProvider();

            // act
            var response = await provider.GetRequiredService<IEMailSender>().SendAsync("john@example.com", "Subject", "Hello");

            // assert
            response.IsSuccess.Should().BeTrue();
            handler.Requests.Should().ContainSingle();
        }

        [Fact]
        public void AddMailgunEmailSender_RegistersHttpClientFactory()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddMailgunEmailSender(new MailgunEmailOptions
            {
                MailgunUser = "api",
                MailgunApiKey = "key",
                MailgunDomain = "mg.example.com",
                EmailFrom = "noreply@example.com"
            });

            // assert
            services.Should().Contain(d => d.ServiceType == typeof(IHttpClientFactory));
        }

        [Fact]
        public void AddUnisenderGoEmailSender_RegistersHttpClientFactory()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddUnisenderGoEmailSender(new UnisenderGoEmailOptions
            {
                ApiKey = "key",
                EmailFrom = "noreply@example.com",
                FromName = "Example"
            });

            // assert
            services.Should().Contain(d => d.ServiceType == typeof(IHttpClientFactory));
        }
    }
}
