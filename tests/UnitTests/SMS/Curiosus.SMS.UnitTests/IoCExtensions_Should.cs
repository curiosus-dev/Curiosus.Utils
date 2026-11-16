#nullable enable

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Curiosus.SMS.Iqsms;
using Curiosus.SMS.Smsc;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
            services.AddSmscSmsSender(o =>
            {
                o.SmscLogin = "login";
                o.SmscPassword = "secret";
            });
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
            var handler = new FakeHttpMessageHandler().Respond(
                HttpStatusCode.OK, "{\"status\":\"ok\",\"messages\":[{\"clientId\":\"1\",\"status\":\"accepted\"}]}");
            var services = new ServiceCollection().AddLogging();
            services.AddIqsmsSender(o =>
            {
                o.Login = "login";
                o.Password = "secret";
            });
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
            services.AddSmscSmsSender();

            // assert
            services.Should().Contain(d => d.ServiceType == typeof(IHttpClientFactory));
        }

        [Fact]
        public void AddIqsmsSender_RegistersHttpClientFactory()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddIqsmsSender();

            // assert
            services.Should().Contain(d => d.ServiceType == typeof(IHttpClientFactory));
        }

        [Fact]
        public void AddSmscSmsSender_UseAsDefaultSenderFalse_RegistersOnlySmscSender()
        {
            var services = new ServiceCollection();

            services.AddSmscSmsSender(useAsDefaultSender: false);

            services.Should().Contain(d => d.ServiceType == typeof(ISmscSender));
            services.Should().NotContain(d => d.ServiceType == typeof(ISmsSender));
        }

        [Fact]
        public void AddIqsmsSender_UseAsDefaultSenderFalse_RegistersOnlyIqsmsSender()
        {
            var services = new ServiceCollection();

            services.AddIqsmsSender(useAsDefaultSender: false);

            services.Should().Contain(d => d.ServiceType == typeof(IIqsmsSender));
            services.Should().NotContain(d => d.ServiceType == typeof(ISmsSender));
        }

        [Fact]
        public async Task AddSmscSmsSender_BindConfiguration_SenderUsesBoundOptions()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Smsc:SmscLogin"] = "login",
                    ["Smsc:SmscPassword"] = "secret",
                    ["Smsc:SmscSender"] = "SIIS",
                },
                services => services.AddSmscSmsSender().BindConfiguration("Smsc"));

            await host.StartAsync();

            var options = host.Services.GetRequiredService<IOptions<SmscOptions>>().Value;
            options.SmscLogin.Should().Be("login");
            options.SmscPassword.Should().Be("secret");
            options.SmscSender.Should().Be("SIIS");
            host.Services.GetRequiredService<ISmsSender>().Should().BeOfType<SmscSender>();
            await host.StopAsync();
        }

        [Fact]
        public async Task AddIqsmsSender_BindConfiguration_SenderUsesBoundOptions()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Iqsms:Login"] = "login",
                    ["Iqsms:Password"] = "secret",
                    ["Iqsms:Sender"] = "SIIS",
                    ["Iqsms:AutoTransformPhoneNumber"] = "false",
                },
                services => services.AddIqsmsSender().BindConfiguration("Iqsms"));

            await host.StartAsync();

            var options = host.Services.GetRequiredService<IOptions<IqsmsOptions>>().Value;
            options.Login.Should().Be("login");
            options.Password.Should().Be("secret");
            options.Sender.Should().Be("SIIS");
            options.AutoTransformPhoneNumber.Should().BeFalse();
            host.Services.GetRequiredService<ISmsSender>().Should().BeOfType<IqsmsSender>();
            await host.StopAsync();
        }

        [Theory]
        [InlineData(null, "secret", "SmscLogin")]
        [InlineData("", "secret", "SmscLogin")]
        [InlineData("   ", "secret", "SmscLogin")]
        [InlineData("login", null, "SmscPassword")]
        [InlineData("login", "", "SmscPassword")]
        public async Task AddSmscSmsSender_InvalidOptions_HostFailsToStart(string? login, string? password, string invalidMember)
        {
            using var host = BuildHost(
                new Dictionary<string, string?> { ["Smsc:SmscLogin"] = login, ["Smsc:SmscPassword"] = password },
                services => services.AddSmscSmsSender().BindConfiguration("Smsc"));

            var act = () => host.StartAsync();

            var exception = (await act.Should().ThrowAsync<OptionsValidationException>()).Which;
            exception.OptionsType.Should().Be(typeof(SmscOptions));
            exception.Message.Should().Contain(invalidMember);
            if (!string.IsNullOrEmpty(password)) exception.Message.Should().NotContain(password);
        }

        [Theory]
        [InlineData(null, "secret", "Login")]
        [InlineData("", "secret", "Login")]
        [InlineData("   ", "secret", "Login")]
        [InlineData("login", null, "Password")]
        [InlineData("login", "", "Password")]
        public async Task AddIqsmsSender_InvalidOptions_HostFailsToStart(string? login, string? password, string invalidMember)
        {
            using var host = BuildHost(
                new Dictionary<string, string?> { ["Iqsms:Login"] = login, ["Iqsms:Password"] = password },
                services => services.AddIqsmsSender().BindConfiguration("Iqsms"));

            var act = () => host.StartAsync();

            var exception = (await act.Should().ThrowAsync<OptionsValidationException>()).Which;
            exception.OptionsType.Should().Be(typeof(IqsmsOptions));
            exception.Message.Should().Contain(invalidMember);
            if (!string.IsNullOrEmpty(password)) exception.Message.Should().NotContain(password);
        }

        [Fact]
        public async Task AddIqsmsSender_ConfigureInCode_InvalidOptions_HostFailsToStart()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>(),
                services => services.AddIqsmsSender(o => o.Login = "login"));

            var act = () => host.StartAsync();

            (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures.Should().ContainSingle()
                .Which.Should().Contain("Password");
        }

        [Fact]
        public async Task AddSmscSmsSender_ConfigureInCode_InvalidOptions_HostFailsToStart()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>(),
                services => services.AddSmscSmsSender(o => o.SmscPassword = "secret"));

            var act = () => host.StartAsync();

            var failure = (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures.Should()
                .ContainSingle().Which;
            failure.Should().Contain("SmscLogin");
            failure.Should().NotContain("secret");
        }

        private static IHost BuildHost(
            IDictionary<string, string?> configuration,
            System.Action<IServiceCollection> configureServices)
        {
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Configuration.AddInMemoryCollection(configuration);
            configureServices(builder.Services);
            return builder.Build();
        }
    }
}
