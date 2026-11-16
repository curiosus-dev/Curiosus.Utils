#nullable enable

using System.Collections.Generic;
using System.Linq;
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
        private static ServiceCollection Services(params (string Key, string Value)[] values)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            return services;
        }

        [Fact]
        public async Task AddReCaptcha_VerifiesThroughNamedHttpClient()
        {
            // arrange
            var handler = new FakeHttpMessageHandler().Respond(HttpStatusCode.OK, "{\"success\":true}");
            var services = Services(("ReCaptcha:ReCaptchaClientKey", "client"), ("ReCaptcha:ReCaptchaServerKey", "server"));
            services.AddReCaptcha();
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
        public void AddReCaptcha_BindsTheDefaultSection()
        {
            // arrange
            var services = Services(
                ("ReCaptcha:ReCaptchaApiUrl", "https://recaptcha.example/verify"),
                ("ReCaptcha:ReCaptchaClientKey", "client"),
                ("ReCaptcha:ReCaptchaServerKey", "server"));

            // act
            services.AddReCaptcha();
            using var provider = services.BuildServiceProvider();

            // assert
            var options = provider.GetRequiredService<IOptions<ReCaptchaOptions>>().Value;
            options.ReCaptchaApiUrl.Should().Be("https://recaptcha.example/verify");
            options.ReCaptchaClientKey.Should().Be("client");
            options.ReCaptchaServerKey.Should().Be("server");
        }

        [Fact]
        public void AddReCaptcha_BindsACustomSection_ThenConfigureOverrides()
        {
            // arrange
            var services = Services(
                ("Web:Captcha:ReCaptchaClientKey", "client"),
                ("Web:Captcha:ReCaptchaServerKey", "server"));

            // act
            services.AddReCaptcha("Web:Captcha", o => o.ReCaptchaServerKey = "from-code");
            using var provider = services.BuildServiceProvider();

            // assert
            var options = provider.GetRequiredService<IOptions<ReCaptchaOptions>>().Value;
            options.ReCaptchaClientKey.Should().Be("client");
            options.ReCaptchaServerKey.Should().Be("from-code");
        }

        [Fact]
        public async Task InvalidOptions_FailTheStart()
        {
            // arrange
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Services.AddReCaptcha(configure: o => o.ReCaptchaClientKey = "client");
            builder.Services.AddReCaptcha();
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
            var values = new Dictionary<string, string>
            {
                ["ReCaptcha:ReCaptchaClientKey"] = "client",
                ["ReCaptcha:ReCaptchaServerKey"] = "server-secret",
                ["ReCaptcha:" + option] = value,
            };
            var services = Services(values.Select(v => (v.Key, v.Value)).ToArray());
            services.AddReCaptcha();
            using var provider = services.BuildServiceProvider();

            // act
            var act = () => provider.GetRequiredService<IOptions<ReCaptchaOptions>>().Value;

            // assert
            var failures = act.Should().Throw<OptionsValidationException>().Which.Failures;
            failures.Should().ContainSingle().Which.Should().Contain(option).And.NotContain("server-secret");
        }
    }
}
