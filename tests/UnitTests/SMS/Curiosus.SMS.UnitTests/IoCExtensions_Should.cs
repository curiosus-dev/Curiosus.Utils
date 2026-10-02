#nullable enable

using System.Net;
using System.Threading.Tasks;
using Curiosus.SMS.Iqsms;
using Curiosus.SMS.Smsc;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using Xunit;

namespace Curiosus.SMS.UnitTests
{
    public class IoCExtensions_Should
    {
        [Fact]
        public async Task AddSmscSmsSender_SendsThroughNamedHttpClient()
        {
            // arrange
            var handler = new FakeHttpMessageHandler().Respond(HttpStatusCode.OK, "{\"id\":1,\"cnt\":1}");
            var services = new ServiceCollection().AddLogging();
            services.AddSmscSmsSender(new SmscOptions { SmscLogin = "login", SmscPassword = "secret" });
            services.AddHttpClient(SmscSender.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            await using var provider = services.BuildServiceProvider();

            // act
            var response = await provider.GetRequiredService<ISmsSender>().SendSmsAsync("79001234567", "text");

            // assert
            response.IsSuccess.Should().BeTrue();
            handler.Requests.Should().ContainSingle();
        }

        [Fact]
        public async Task AddIqsmsSender_SendsThroughNamedHttpClient()
        {
            // arrange
            var handler = new FakeHttpMessageHandler().Respond(HttpStatusCode.OK, "accepted;A1", "text/plain");
            var services = new ServiceCollection().AddLogging();
            services.AddIqsmsSender(new IqsmsOptions { Login = "login", Password = "secret" });
            services.AddHttpClient(IqsmsSender.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            await using var provider = services.BuildServiceProvider();

            // act
            var response = await provider.GetRequiredService<ISmsSender>().SendSmsAsync("+79001234567", "text");

            // assert
            response.IsSuccess.Should().BeTrue();
            handler.Requests.Should().ContainSingle();
        }

        [Fact]
        public void AddSmscSmsSender_RegistersHttpClientFactory()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddSmscSmsSender(new SmscOptions { SmscLogin = "login", SmscPassword = "secret" });

            // assert
            services.Should().Contain(d => d.ServiceType == typeof(IHttpClientFactory));
        }

        [Fact]
        public void AddIqsmsSender_RegistersHttpClientFactory()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddIqsmsSender(new IqsmsOptions { Login = "login", Password = "secret" });

            // assert
            services.Should().Contain(d => d.ServiceType == typeof(IHttpClientFactory));
        }
    }
}
