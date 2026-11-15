using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Curiosus.Hosting.Web.UnitTests
{
    public sealed class CuriosusWebHostTests
    {
        [Fact]
        public void CreateBuilder_ShortConfigSwitch_IsTheConfigKey()
        {
            var builder = CuriosusWebHost.CreateBuilder(["-c", "/etc/app"]);

            builder.Configuration[CuriosusHost.ConfigurationPathKey].Should().Be("/etc/app");
        }

        [Fact]
        public async Task AddCuriosusDefaults_WorksOnAWebApplicationBuilder()
        {
            var builder = CuriosusWebHost.CreateBuilder(["--urls", "http://127.0.0.1:0"])
                .AddCuriosusDefaults(o =>
                {
                    o.IsConfigurationOptional = true;
                    o.LogConfigurationOnStart = false;
                });
            var app = builder.Build();
            app.MapGet("/", () => "ok");

            var code = await CuriosusApp.RunAsync(app, (services, _) =>
                Task.FromResult(services.GetService<System.TimeProvider>() != null ? 0 : 1));

            code.Should().Be(CuriosusExitCodes.Success);
        }
    }
}
