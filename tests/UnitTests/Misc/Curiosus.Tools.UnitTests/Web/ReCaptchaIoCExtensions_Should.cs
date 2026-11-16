#nullable enable

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Curiosus.Tools.Web.ReCaptcha;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net.Http;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web
{
    public class ReCaptchaIoCExtensions_Should
    {
        [Fact]
        public async Task AddReCaptcha_VerifiesThroughNamedHttpClient()
        {
            // arrange
            var handler = new FakeHttpMessageHandler().Respond(HttpStatusCode.OK, "{\"success\":true}");
            var services = new ServiceCollection();
            services.AddReCaptcha(o =>
            {
                o.ReCaptchaClientKey = "client";
                o.ReCaptchaServerKey = "server";
            });
            services.AddHttpClient(ReCaptchaService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            await using var provider = services.BuildServiceProvider();

            // act
            var result = await provider.GetRequiredService<ReCaptchaService>().VerifyReCaptchaAsync("token");

            // assert
            result.Should().BeTrue();
            handler.Requests.Should().ContainSingle()
                .Which.Uri.Should().Be(new System.Uri("https://www.google.com/recaptcha/api/siteverify"));
        }

        [Fact]
        public void AddReCaptcha_RegistersHttpClientFactory()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddReCaptcha();

            // assert
            services.Should().Contain(d => d.ServiceType == typeof(IHttpClientFactory));
        }

        [Fact]
        public void AddReCaptcha_BindsConfiguration()
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReCaptcha:ReCaptchaApiUrl"] = "https://recaptcha.example/verify",
                    ["ReCaptcha:ReCaptchaClientKey"] = "client",
                    ["ReCaptcha:ReCaptchaServerKey"] = "server",
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);

            // act
            services.AddReCaptcha().BindConfiguration("ReCaptcha");
            using var provider = services.BuildServiceProvider();

            // assert
            var options = provider.GetRequiredService<IOptions<ReCaptchaOptions>>().Value;
            options.ReCaptchaApiUrl.Should().Be("https://recaptcha.example/verify");
            options.ReCaptchaClientKey.Should().Be("client");
            options.ReCaptchaServerKey.Should().Be("server");
        }

        [Fact]
        public async Task InvalidOptions_FailTheStart()
        {
            // arrange
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddReCaptcha(o => o.ReCaptchaClientKey = "client");
            using var host = builder.Build();

            // act
            var act = () => host.StartAsync();

            // assert
            (await act.Should().ThrowAsync<OptionsValidationException>())
                .Which.Failures.Should().ContainSingle(f => f.Contains(nameof(ReCaptchaOptions.ReCaptchaServerKey)));
        }

        public static TheoryData<string, string> InvalidValues => new()
        {
            { nameof(ReCaptchaOptions.ReCaptchaApiUrl), "" },
            { nameof(ReCaptchaOptions.ReCaptchaApiUrl), "/recaptcha/api/siteverify" },
            { nameof(ReCaptchaOptions.ReCaptchaClientKey), " " },
            { nameof(ReCaptchaOptions.ReCaptchaServerKey), "" },
            { nameof(ReCaptchaOptions.RetryTimeoutSec), "-1" },
            { nameof(ReCaptchaOptions.RetryCount), "-1" },
        };

        [Theory]
        [MemberData(nameof(InvalidValues))]
        public void InvalidValue_FailsValidation(string option, string value)
        {
            // arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReCaptcha:ReCaptchaClientKey"] = "client",
                    ["ReCaptcha:ReCaptchaServerKey"] = "server-secret",
                    ["ReCaptcha:" + option] = value,
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddReCaptcha().BindConfiguration("ReCaptcha");
            using var provider = services.BuildServiceProvider();

            // act
            var act = () => provider.GetRequiredService<IOptions<ReCaptchaOptions>>().Value;

            // assert
            var failures = act.Should().Throw<OptionsValidationException>().Which.Failures;
            failures.Should().ContainSingle().Which.Should().Contain(option).And.NotContain("server-secret");
        }
    }
}
