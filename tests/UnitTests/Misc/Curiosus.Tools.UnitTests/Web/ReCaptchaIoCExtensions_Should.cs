#nullable enable

using System.Net;
using System.Threading.Tasks;
using Curiosus.Tools.Web.ReCaptcha;
using Curiosus.UnitTests.Shared;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
            services.AddReCaptcha(new ReCaptchaOptions
            {
                ReCaptchaApiUrl = "https://www.google.com/recaptcha/api/siteverify",
                ReCaptchaClientKey = "client",
                ReCaptchaServerKey = "server"
            });
            services.AddHttpClient(ReCaptchaService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
            await using var provider = services.BuildServiceProvider();

            // act
            var result = await provider.GetRequiredService<ReCaptchaService>().VerifyReCaptchaAsync("token");

            // assert
            result.Should().BeTrue();
            handler.Requests.Should().ContainSingle();
        }

        [Fact]
        public void AddReCaptcha_RegistersHttpClientFactory()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            services.AddReCaptcha(new ReCaptchaOptions
            {
                ReCaptchaApiUrl = "https://example.com",
                ReCaptchaClientKey = "client",
                ReCaptchaServerKey = "server"
            });

            // assert
            services.Should().Contain(d => d.ServiceType == typeof(IHttpClientFactory));
        }
    }
}
