using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Curiosus.Hosting.UnitTests
{
    [Collection(ProcessStateCollection.Name)]
    public sealed class CuriosusHostTests : IDisposable
    {
        private readonly string? _dotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        private readonly string? _aspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        [Theory]
        [InlineData(null, null, "Production")]
        [InlineData("Staging", null, "Staging")]
        [InlineData(null, "Development", "Development")]
        [InlineData("Staging", "Development", "Staging")]
        public void CreateBuilder_ResolvesTheEnvironment(string? dotnet, string? aspNetCore, string expected)
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", dotnet);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", aspNetCore);

            var builder = CuriosusHost.CreateBuilder([]);

            builder.Environment.EnvironmentName.Should().Be(expected);
        }

        [Fact]
        public void CreateBuilder_EnvironmentArgument_BeatsTheAspNetCoreFallback()
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            var builder = CuriosusHost.CreateBuilder(["--environment", "Development"]);

            builder.Environment.EnvironmentName.Should().Be("Development");
        }

        [Fact]
        public void CreateBuilder_ContentRoot_IsTheBinariesDirectory()
        {
            var builder = CuriosusHost.CreateBuilder([]);

            Path.TrimEndingDirectorySeparator(builder.Environment.ContentRootPath)
                .Should().Be(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        }

        [Fact]
        public void CreateBuilder_ContentRootArgument_Wins()
        {
            var dir = Path.GetTempPath();

            var builder = CuriosusHost.CreateBuilder(["--contentRoot", dir]);

            Path.TrimEndingDirectorySeparator(builder.Environment.ContentRootPath)
                .Should().Be(Path.TrimEndingDirectorySeparator(dir));
        }

        [Fact]
        public void CreateBuilder_ShortConfigSwitch_IsTheConfigKey()
        {
            var builder = CuriosusHost.CreateBuilder(["-c", "/etc/app", "--other", "x"]);

            builder.Configuration[CuriosusHost.ConfigurationPathKey].Should().Be("/etc/app");
            builder.Configuration["other"].Should().Be("x");
        }

        [Fact]
        public void MapShortSwitches_MapsBothForms()
        {
            CuriosusHost.MapShortSwitches(["-c", "a", "-c=b", "--config", "c", "-cx"])
                .Should().Equal("--config", "a", "--config=b", "--config", "c", "-cx");
        }

        [Theory]
        [InlineData("-v")]
        [InlineData("--version")]
        public void VersionSwitch_WritesTheVersion(string arg)
        {
            var output = new System.IO.StringWriter();

            CuriosusHost.TryWriteVersion(["-c", "x", arg], output).Should().BeTrue();

            output.ToString().Should().MatchRegex(@"^App version: \d+\.\d+\.\d+\.\d+");
        }

        [Fact]
        public void NoVersionSwitch_WritesNothing()
        {
            var output = new System.IO.StringWriter();

            CuriosusHost.TryWriteVersion(["-c", "x", "--verbose"], output).Should().BeFalse();

            output.ToString().Should().BeEmpty();
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _dotnetEnvironment);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _aspNetCoreEnvironment);
        }
    }
}
