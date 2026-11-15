using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Curiosus.Hosting.UnitTests
{
    [Collection(ProcessStateCollection.Name)]
    public sealed class AddCuriosusConfigurationTests : IDisposable
    {
        private readonly TempDirectory _dir = new();
        private readonly string _variable = "CURIOSUS_TEST_" + Guid.NewGuid().ToString("N");

        private HostApplicationBuilder CreateBuilder(string environment, params string[] args) =>
            Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = _dir.Path,
                EnvironmentName = environment,
                Args = args,
            });

        [Fact]
        public void FilesAreLayered_EnvironmentVariablesAndCommandLineWin()
        {
            _dir.Write("config.yml", "A: base\nB: base\nC: base\nD: base\nE: base\nF: base\n");
            _dir.Write("config.secret.yml", "B: secret\n");
            _dir.Write("config.Development.yml", "C: env\n");
            _dir.Write($"config.Development.{Environment.UserName}.yml", "E: user\n");
            _dir.Write("config.yml.bak", "F: ignored\n");
            Environment.SetEnvironmentVariable(_variable, "from-env");
            _dir.Write("config.Development.secret.yml", $"D: env-secret\n{_variable}: from-yaml\nCli: from-yaml\n");

            var builder = CreateBuilder("Development", "--Cli=from-cli").AddCuriosusConfiguration();
            var configuration = builder.Configuration;

            configuration["A"].Should().Be("base");
            configuration["B"].Should().Be("secret");
            configuration["C"].Should().Be("env");
            configuration["D"].Should().Be("env-secret");
            configuration["E"].Should().Be("user");
            configuration["F"].Should().Be("base");
            configuration[_variable].Should().Be("from-env");
            configuration["Cli"].Should().Be("from-cli");
        }

        [Fact]
        public void UserSecrets_OverrideTheFiles()
        {
            _dir.Write("config.yml", "Password: placeholder\n");
            var secrets = new TempDirectory();
            try
            {
                secrets.Write("secrets.json", "{ \"Password\": \"from-secrets\" }");
                var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
                {
                    ContentRootPath = _dir.Path,
                    EnvironmentName = "Production",
                });
                // the same source AddUserSecrets adds
                builder.Configuration.AddJsonFile(
                    new Microsoft.Extensions.FileProviders.PhysicalFileProvider(secrets.Path), "secrets.json", false, false);

                builder.AddCuriosusConfiguration();

                builder.Configuration["Password"].Should().Be("from-secrets");
            }
            finally
            {
                secrets.Dispose();
            }
        }

        [Fact]
        public void PersonalFile_IsNotLoadedInProduction()
        {
            _dir.Write("config.yml", "E: base\n");
            _dir.Write($"config.Production.{Environment.UserName}.yml", "E: user\n");

            var builder = CreateBuilder("Production").AddCuriosusConfiguration();

            builder.Configuration["E"].Should().Be("base");
        }

        [Fact]
        public void ConfigSwitch_PointsToADirectory()
        {
            _dir.Write("config.yml", "A: base\n");
            _dir.Write("other/config.yml", "A: other\n");

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = _dir.Path,
                EnvironmentName = "Production",
                Args = CuriosusHost.MapShortSwitches(["-c", "other"]),
            }).AddCuriosusConfiguration();

            builder.Configuration["A"].Should().Be("other");
        }

        [Fact]
        public void SingleFile_IsLoadedAlone()
        {
            _dir.Write("config.yml", "A: base\nB: base\n");
            _dir.Write("config.Development.yml", "B: env\n");
            var single = _dir.Write("custom.yml", "A: single\n");

            var builder = CreateBuilder("Development", "--config", single).AddCuriosusConfiguration();

            builder.Configuration["A"].Should().Be("single");
            builder.Configuration["B"].Should().BeNull();
        }

        [Fact]
        public void MissingConfigFile_Throws_UnlessOptional()
        {
            var act = () => CreateBuilder("Production").AddCuriosusConfiguration();
            act.Should().Throw<FileNotFoundException>();

            var optional = () => CreateBuilder("Production").AddCuriosusConfiguration(optional: true);
            optional.Should().NotThrow();
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_variable, null);
            _dir.Dispose();
        }
    }
}
