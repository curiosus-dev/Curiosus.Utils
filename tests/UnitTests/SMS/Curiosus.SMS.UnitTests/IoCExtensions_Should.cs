#nullable enable

using System;
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
            var services = ServicesWithConfiguration(new Dictionary<string, string?>
            {
                ["Smsc:SmscLogin"] = "login",
                ["Smsc:SmscPassword"] = "secret",
            });
            services.AddSmscSmsSender();
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
            var services = ServicesWithConfiguration(new Dictionary<string, string?>
            {
                ["Iqsms:Login"] = "login",
                ["Iqsms:Password"] = "secret",
            });
            services.AddIqsmsSender();
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
            var services = new ServiceCollection();

            services.AddSmscSmsSender();

            services.Should().Contain(d => d.ServiceType == typeof(IHttpClientFactory));
        }

        [Fact]
        public void AddIqsmsSender_RegistersHttpClientFactory()
        {
            var services = new ServiceCollection();

            services.AddIqsmsSender();

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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void AddSmscSmsSender_EmptySectionPath_Throws(string? sectionPath)
        {
            var act = () => new ServiceCollection().AddSmscSmsSender(sectionPath!);

            act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("configSectionPath");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void AddIqsmsSender_EmptySectionPath_Throws(string? sectionPath)
        {
            var act = () => new ServiceCollection().AddIqsmsSender(sectionPath!);

            act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("configSectionPath");
        }

        [Fact]
        public async Task AddSmscSmsSender_BindsDefaultSection()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Smsc:SmscLogin"] = "login",
                    ["Smsc:SmscPassword"] = "secret",
                    ["Smsc:SmscSender"] = "SIIS",
                },
                services => services.AddSmscSmsSender());

            await host.StartAsync();

            SmscOptions.SectionName.Should().Be("Smsc");
            var options = host.Services.GetRequiredService<IOptions<SmscOptions>>().Value;
            options.SmscLogin.Should().Be("login");
            options.SmscPassword.Should().Be("secret");
            options.SmscSender.Should().Be("SIIS");
            host.Services.GetRequiredService<ISmsSender>().Should().BeOfType<SmscSender>();
            await host.StopAsync();
        }

        [Fact]
        public async Task AddIqsmsSender_BindsDefaultSection()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Iqsms:Login"] = "login",
                    ["Iqsms:Password"] = "secret",
                    ["Iqsms:Sender"] = "SIIS",
                    ["Iqsms:AutoTransformPhoneNumber"] = "false",
                },
                services => services.AddIqsmsSender());

            await host.StartAsync();

            IqsmsOptions.SectionName.Should().Be("Iqsms");
            var options = host.Services.GetRequiredService<IOptions<IqsmsOptions>>().Value;
            options.Login.Should().Be("login");
            options.Password.Should().Be("secret");
            options.Sender.Should().Be("SIIS");
            options.AutoTransformPhoneNumber.Should().BeFalse();
            host.Services.GetRequiredService<ISmsSender>().Should().BeOfType<IqsmsSender>();
            await host.StopAsync();
        }

        [Fact]
        public async Task AddSmscSmsSender_BindsCustomSectionPath()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Smsc:SmscLogin"] = "default-login",
                    ["Sms:Providers:Smsc:SmscLogin"] = "login",
                    ["Sms:Providers:Smsc:SmscPassword"] = "secret",
                },
                services => services.AddSmscSmsSender("Sms:Providers:Smsc"));

            await host.StartAsync();

            var options = host.Services.GetRequiredService<IOptions<SmscOptions>>().Value;
            options.SmscLogin.Should().Be("login");
            options.SmscPassword.Should().Be("secret");
            await host.StopAsync();
        }

        [Fact]
        public async Task AddIqsmsSender_BindsCustomSectionPath()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Iqsms:Login"] = "default-login",
                    ["Sms:Providers:Iqsms:Login"] = "login",
                    ["Sms:Providers:Iqsms:Password"] = "secret",
                },
                services => services.AddIqsmsSender("Sms:Providers:Iqsms"));

            await host.StartAsync();

            var options = host.Services.GetRequiredService<IOptions<IqsmsOptions>>().Value;
            options.Login.Should().Be("login");
            options.Password.Should().Be("secret");
            await host.StopAsync();
        }

        [Fact]
        public async Task AddSmscSmsSender_ConfigureOverridesBoundValue()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Smsc:SmscLogin"] = "login",
                    ["Smsc:SmscPassword"] = "secret",
                    ["Smsc:SmscSender"] = "SIIS",
                },
                services => services.AddSmscSmsSender(configure: o => o.SmscSender = "Other"));

            await host.StartAsync();

            var options = host.Services.GetRequiredService<IOptions<SmscOptions>>().Value;
            options.SmscSender.Should().Be("Other");
            options.SmscLogin.Should().Be("login");
            await host.StopAsync();
        }

        [Fact]
        public async Task AddIqsmsSender_ConfigureOverridesBoundValue()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Iqsms:Login"] = "login",
                    ["Iqsms:Password"] = "secret",
                    ["Iqsms:Sender"] = "SIIS",
                },
                services => services.AddIqsmsSender(configure: o => o.Sender = "Other"));

            await host.StartAsync();

            var options = host.Services.GetRequiredService<IOptions<IqsmsOptions>>().Value;
            options.Sender.Should().Be("Other");
            options.Login.Should().Be("login");
            await host.StopAsync();
        }

        [Fact]
        public async Task AddSmscSmsSender_NoSection_ConfigureInCodeIsEnough()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>(),
                services => services.AddSmscSmsSender(configure: o =>
                {
                    o.SmscLogin = "login";
                    o.SmscPassword = "secret";
                }));

            await host.StartAsync();

            host.Services.GetRequiredService<IOptions<SmscOptions>>().Value.SmscLogin.Should().Be("login");
            await host.StopAsync();
        }

        [Fact]
        public async Task AddIqsmsSender_NoSection_ConfigureInCodeIsEnough()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>(),
                services => services.AddIqsmsSender(configure: o =>
                {
                    o.Login = "login";
                    o.Password = "secret";
                }));

            await host.StartAsync();

            host.Services.GetRequiredService<IOptions<IqsmsOptions>>().Value.Login.Should().Be("login");
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
                services => services.AddSmscSmsSender());

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
                services => services.AddIqsmsSender());

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
                services => services.AddIqsmsSender(configure: o => o.Login = "login"));

            var act = () => host.StartAsync();

            (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures.Should().ContainSingle()
                .Which.Should().Contain("Password");
        }

        [Fact]
        public async Task AddSmscSmsSender_ConfigureInCode_InvalidOptions_HostFailsToStart()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>(),
                services => services.AddSmscSmsSender(configure: o => o.SmscPassword = "secret"));

            var act = () => host.StartAsync();

            var failure = (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures.Should()
                .ContainSingle().Which;
            failure.Should().Contain("SmscLogin");
            failure.Should().NotContain("secret");
        }

        [Fact]
        public async Task AddSmscSmsSender_CalledTwice_ReportsErrorsOnce()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>(),
                services =>
                {
                    services.AddSmscSmsSender();
                    services.AddSmscSmsSender();
                });

            var act = () => host.StartAsync();

            var failures = (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures;
            // one failure per invalid member, not one per call
            failures.Should().HaveCount(2);
            failures.Should().ContainSingle(f => f.Contains("'SmscLogin'"));
            failures.Should().ContainSingle(f => f.Contains("'SmscPassword'"));
        }

        [Fact]
        public async Task AddIqsmsSender_CalledTwice_ReportsErrorsOnce()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>(),
                services =>
                {
                    services.AddIqsmsSender();
                    services.AddIqsmsSender();
                });

            var act = () => host.StartAsync();

            var failures = (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures;
            // one failure per invalid member, not one per call
            failures.Should().HaveCount(2);
            failures.Should().ContainSingle(f => f.Contains("'Login'"));
            failures.Should().ContainSingle(f => f.Contains("'Password'"));
        }

        [Fact]
        public async Task AddSmscSmsSender_CalledTwice_BindsFirstSectionOnly_AppliesBothConfigures()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Smsc:SmscLogin"] = "login",
                    ["Smsc:SmscPassword"] = "secret",
                    ["Other:SmscLogin"] = "other-login",
                },
                services =>
                {
                    services.AddSmscSmsSender();
                    services.AddSmscSmsSender("Other", o => o.SmscSender = "SIIS");
                });

            await host.StartAsync();

            var options = host.Services.GetRequiredService<IOptions<SmscOptions>>().Value;
            options.SmscLogin.Should().Be("login");
            options.SmscSender.Should().Be("SIIS");
            await host.StopAsync();
        }

        [Fact]
        public async Task AddIqsmsSender_CalledTwice_BindsFirstSectionOnly_AppliesBothConfigures()
        {
            using var host = BuildHost(
                new Dictionary<string, string?>
                {
                    ["Iqsms:Login"] = "login",
                    ["Iqsms:Password"] = "secret",
                    ["Other:Login"] = "other-login",
                },
                services =>
                {
                    services.AddIqsmsSender();
                    services.AddIqsmsSender("Other", o => o.Sender = "SIIS");
                });

            await host.StartAsync();

            var options = host.Services.GetRequiredService<IOptions<IqsmsOptions>>().Value;
            options.Login.Should().Be("login");
            options.Sender.Should().Be("SIIS");
            await host.StopAsync();
        }

        private static IServiceCollection ServicesWithConfiguration(IDictionary<string, string?> configuration)
        {
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
            return services;
        }

        private static IHost BuildHost(
            IDictionary<string, string?> configuration,
            Action<IServiceCollection> configureServices)
        {
            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.Configuration.AddInMemoryCollection(configuration);
            configureServices(builder.Services);
            return builder.Build();
        }
    }
}
