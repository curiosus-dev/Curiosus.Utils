using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Targets;
using Xunit;

namespace Curiosus.Hosting.UnitTests
{
    [Collection(NLogCollection.Name)]
    public sealed class AddCuriosusNLogTests : IDisposable
    {
        private const string MemoryConfig = """
            <nlog throwExceptions="true">
              <targets>
                <target name="mem" type="Memory" layout="${logger}|${message}|${var:outputdir}|${var:appname}" />
              </targets>
              <rules>
                <logger name="*" minlevel="Trace" writeTo="mem" />
              </rules>
            </nlog>
            """;

        private readonly string _contentRoot =
            Path.Combine(Path.GetTempPath(), "curiosus-logging-" + Guid.NewGuid().ToString("N"));

        public AddCuriosusNLogTests()
        {
            Directory.CreateDirectory(_contentRoot);
        }

        [Fact]
        public void LoadsConfigurationRelativeToContentRoot()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "NLog.config"), MemoryConfig);
            var builder = CreateBuilder();

            builder.AddCuriosusNLog();
            using var host = builder.Build();
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

            Logs().Should().ContainSingle(l => l.StartsWith("test|hello|"));
        }

        [Fact]
        public void LogConfigurationPath_ComesFromTheLogSection()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "custom.config"), MemoryConfig);
            var builder = CreateBuilder(("Log:LogConfigurationPath", "custom.config"));

            builder.AddCuriosusNLog();
            using var host = builder.Build();
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

            Logs().Should().ContainSingle(l => l.StartsWith("test|hello|"));
        }

        [Fact]
        public void SetsOutputDirectoryAsAbsolutePathAndAppName()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "NLog.config"), MemoryConfig);
            var builder = CreateBuilder(("AppName", "my-app"), ("Log:LogOutputDirectory", "logs/out"));

            builder.AddCuriosusNLog();
            using var host = builder.Build();
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

            Logs().Should().ContainSingle()
                .Which.Should().Be($"test|hello|{Path.Combine(_contentRoot, "logs", "out")}|my-app");
        }

        [Fact]
        public void AppName_FallsBackToApplicationName()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "NLog.config"), MemoryConfig);
            var builder = CreateBuilder();

            builder.AddCuriosusNLog();
            using var host = builder.Build();
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

            Logs().Should().ContainSingle().Which.Should().EndWith("|" + builder.Environment.ApplicationName);
        }

        [Fact]
        public void VariablesNotDeclaredInConfigFile_AreNotAnError()
        {
            File.WriteAllText(
                Path.Combine(_contentRoot, "NLog.config"),
                MemoryConfig.Replace("|${var:outputdir}|${var:appname}", string.Empty));
            var builder = CreateBuilder();

            var act = () => builder.AddCuriosusNLog();

            act.Should().NotThrow();
        }

        [Fact]
        public void ConfigSettings_AreReadFromYamlAndEnvironmentVariables()
        {
            var variable = "CURIOSUS_TEST_" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(variable, "from-env");
            try
            {
                File.WriteAllText(Path.Combine(_contentRoot, "config.yml"), "Mail:\n  To: from-yaml@example.com\n");
                File.WriteAllText(
                    Path.Combine(_contentRoot, "NLog.config"),
                    MemoryConfig.Replace(
                        "${var:appname}",
                        $"${{configsetting:item=Mail.To}}|${{configsetting:item={variable}}}"));
                var builder = CreateBuilder();
                builder.Configuration.AddYamlFile(Path.Combine(_contentRoot, "config.yml"));
                builder.Configuration.AddEnvironmentVariables();

                builder.AddCuriosusNLog();
                using var host = builder.Build();
                host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

                Logs().Should().ContainSingle().Which.Should().EndWith("|from-yaml@example.com|from-env");
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }

        [Fact]
        public async Task DoesNotWriteStartAndStopMessages()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "NLog.config"), MemoryConfig);
            var builder = CreateBuilder(("AppName", "my-app"));

            builder.AddCuriosusNLog();
            using var host = builder.Build();
            await host.StartAsync();
            await host.StopAsync();

            Logs().Should().NotContain(l => l.StartsWith("app"));
        }

        [Fact]
        public async Task AppLifecycleLogging_WritesStartAndStopMessagesToTheLifecycleLoggers()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "NLog.config"), MemoryConfig);
            var builder = CreateBuilder(("AppName", "my-app"));

            builder.AddCuriosusNLog();
            builder.Services.AddCuriosusAppLifecycleLogging();
            using var host = builder.Build();
            await host.StartAsync();
            await host.StopAsync();

            Logs().Where(l => l.StartsWith("app"))
                .Select(l => string.Join('|', l.Split('|').Take(2))).Should().Equal(
                "appStartEmailLog|my-app is started",
                "appStopEmailLog|my-app is stopped");
        }

        [Fact]
        public void MissingConfigFile_Throws()
        {
            var builder = CreateBuilder();

            var act = () => builder.AddCuriosusNLog();

            act.Should().Throw<Exception>();
        }

        [Fact]
        public void EmptyConfigurationPath_Throws()
        {
            var builder = CreateBuilder(("Log:LogConfigurationPath", ""));

            var act = () => builder.AddCuriosusNLog();

            act.Should().Throw<OptionsValidationException>();
        }

        [Fact]
        public void ConfigureDelegate_OverridesTheLogSection()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "other.config"), MemoryConfig);
            var builder = CreateBuilder(("Log:LogConfigurationPath", "missing.config"));

            builder.AddCuriosusNLog(configure: o => o.LogConfigurationPath = "other.config");
            using var host = builder.Build();
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

            Logs().Should().ContainSingle();
        }

        [Fact]
        public void CustomSectionPath_IsRead()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "custom.config"), MemoryConfig);
            var builder = CreateBuilder(
                ("Logging:NLog:LogConfigurationPath", "custom.config"),
                ("Logging:NLog:LogOutputDirectory", "custom-log"));

            builder.AddCuriosusNLog("Logging:NLog");
            using var host = builder.Build();
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

            Logs().Should().ContainSingle().Which.Should().Contain(Path.Combine(_contentRoot, "custom-log"));
            host.Services.GetRequiredService<IOptions<CuriosusNLogOptions>>().Value.LogConfigurationPath
                .Should().Be("custom.config");
        }

        [Fact]
        public void ConfigureDelegate_AppliesToTheOptionsToo()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "NLog.config"), MemoryConfig);
            var builder = CreateBuilder(("Log:LogOutputDirectory", "from-section"));

            builder.AddCuriosusNLog(configure: o => o.LogOutputDirectory = "from-code");
            using var host = builder.Build();

            host.Services.GetRequiredService<IOptions<CuriosusNLogOptions>>().Value.LogOutputDirectory
                .Should().Be("from-code");
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void EmptySectionPath_Throws(string path)
        {
            var builder = CreateBuilder();

            var act = () => builder.AddCuriosusNLog(path);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void CalledTwice_SetsNLogUpOnce_SecondConfigureReachesOnlyTheOptions()
        {
            File.WriteAllText(Path.Combine(_contentRoot, "NLog.config"), MemoryConfig);
            var builder = CreateBuilder(("Log:LogOutputDirectory", "first"));

            builder.AddCuriosusNLog();
            var configuration = LogManager.Configuration;
            var act = () => builder.AddCuriosusNLog("Missing", o => o.LogConfigurationPath = "missing.config");

            act.Should().NotThrow("the second call doesn't load NLog.config again");
            LogManager.Configuration.Should().BeSameAs(configuration);
            builder.Services.Count(d => d.ServiceType == typeof(IValidateOptions<CuriosusNLogOptions>)).Should().Be(1);
            using var host = builder.Build();
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

            Logs().Should().ContainSingle().Which.Should().Contain(Path.Combine(_contentRoot, "first"));
            var options = host.Services.GetRequiredService<IOptions<CuriosusNLogOptions>>().Value;
            options.LogConfigurationPath.Should().Be("missing.config");
            options.LogOutputDirectory.Should().Be("first", "the section is not bound again");
        }

        public void Dispose()
        {
            LogManager.Shutdown();
            LogManager.Configuration = null;
            if (Directory.Exists(_contentRoot))
                Directory.Delete(_contentRoot, recursive: true);
        }

        private HostApplicationBuilder CreateBuilder(params (string Key, string Value)[] settings)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = _contentRoot,
                DisableDefaults = true,
            });
            builder.Configuration.AddInMemoryCollection(
                settings.Select(s => new System.Collections.Generic.KeyValuePair<string, string?>(s.Key, s.Value)));

            return builder;
        }

        private static string[] Logs() =>
            LogManager.Configuration!.FindTargetByName<MemoryTarget>("mem")!.Logs.ToArray();
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class NLogCollection
    {
        public const string Name = "NLog global configuration";
    }
}
