using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Configuration;
using FluentAssertions;
using NLog;
using Xunit;

namespace Curiosus.Hosting.UnitTests
{
    [Collection(NLogCollection.Name)]
    public sealed class NLogConfigurationTests : IDisposable
    {
        private readonly string _outputDirectory = Path.Combine(Path.GetTempPath(), "curiosus-nlog-" + Guid.NewGuid().ToString("N"));
        private readonly TestBootstrapper _bootstrapper = new();

        [Fact]
        public void SampleConfig_LoadsAndWritesToConfiguredDirectory()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "NLog.config");

            new CuriosusNLogConfigurator(configPath, _bootstrapper.LoadLoggingConfiguration)
                .WithLogOutputDirectory(_outputDirectory)
                .WithAppName("tests")
                .Configure();

            LogManager.GetLogger("main").Info("hello");
            LogManager.Flush();

            File.ReadAllText(Path.Combine(_outputDirectory, "main.log")).Should().Contain("hello");
        }

        [Fact]
        public void MissingConfigFile_Throws()
        {
            var configPath = Path.Combine(_outputDirectory, "missing.config");

            var act = () => new CuriosusNLogConfigurator(configPath, _bootstrapper.LoadLoggingConfiguration);

            act.Should().Throw<Exception>();
        }

        [Fact]
        public void MissingVariable_Throws()
        {
            Directory.CreateDirectory(_outputDirectory);
            var configPath = Path.Combine(_outputDirectory, "NLog.config");
            File.WriteAllText(configPath, "<nlog><targets><target name=\"null\" type=\"Null\" /></targets></nlog>");

            var configurator = new CuriosusNLogConfigurator(configPath, _bootstrapper.LoadLoggingConfiguration);

            var act = () => configurator.WithAppName("tests");

            act.Should().Throw<Exception>().WithMessage("*\"appname\"*");
        }

        public void Dispose()
        {
            LogManager.Shutdown();
            LogManager.Configuration = null;
            if (Directory.Exists(_outputDirectory))
                Directory.Delete(_outputDirectory, recursive: true);
        }

        private sealed class TestBootstrapper : CuriosusAppBootstrapper<CuriosusCLIArguments, CuriosusAppConfiguration>
        {
            public new void LoadLoggingConfiguration(string path) => base.LoadLoggingConfiguration(path);

            protected override Task<int> RunInternalAsync(
                string[] rawArguments,
                CuriosusCLIArguments arguments,
                CuriosusAppConfiguration configuration,
                IConfigurationProvider<CuriosusAppConfiguration> configurationProvider,
                string customContentRootDirectory,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(0);
        }
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class NLogCollection
    {
        public const string Name = "NLog global configuration";
    }
}
