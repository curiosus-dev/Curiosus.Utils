using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Tools;
using Curiosus.Tools.AppInitializer;
using Curiosus.Tools.Performance;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Curiosus.Hosting.UnitTests
{
    [Collection(ProcessStateCollection.Name)]
    public sealed class AddCuriosusDefaultsTests : IDisposable
    {
        private readonly TempDirectory _dir = new();
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo? _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        private readonly CultureInfo? _defaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        private HostApplicationBuilder CreateBuilder(string config)
        {
            _dir.Write("config.yml", config);
            return Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = _dir.Path,
                EnvironmentName = "Production",
            }).AddCuriosusDefaults(o => o.LogConfigurationOnStart = false);
        }

        [Fact]
        public async Task Initializers_RunBeforeHostedServices()
        {
            var calls = new List<string>();
            var builder = CreateBuilder("AppName: test\n");
            builder.Services.AddSingleton(calls);
            builder.Services.AddHostedService<RecordingService>();
            builder.Services.AddAppInitializer<RecordingInitializer>();

            using var host = builder.Build();
            await host.StartAsync();
            await host.StopAsync();

            calls.Should().Equal("initializer", "hosted service");
        }

        [Fact]
        public async Task CultureSection_IsAppliedProcessWide()
        {
            var builder = CreateBuilder("Culture:\n  DefaultCulture: de-DE\n");

            using var host = builder.Build();
            await host.StartAsync();
            await host.StopAsync();

            CultureInfo.DefaultThreadCurrentCulture!.Name.Should().Be("de-DE");
            CultureInfo.DefaultThreadCurrentUICulture!.Name.Should().Be("de-DE");
        }

        [Fact]
        public async Task UniqueIdGenerator_IsInitializedFromConfiguration()
        {
            var builder = CreateBuilder("UniqueId:\n  GeneratorId: 7\n");

            using var host = builder.Build();
            await host.StartAsync();
            await host.StopAsync();

            ((UniqueIdGenerator.Generate() >> 12) & 1023).Should().Be(7);
        }

        [Fact]
        public void RegistersTheSystemClockAndThePerformanceMeasurer()
        {
            using var host = CreateBuilder("AppName: test\n").Build();

            host.Services.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
            host.Services.GetService<PerformanceMeasurer>().Should().NotBeNull();
        }

        [Fact]
        public void RegisteredClock_IsKept()
        {
            var fake = new FakeTimeProvider();
            _dir.Write("config.yml", "AppName: test\n");
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = _dir.Path });
            builder.Services.AddSingleton<TimeProvider>(fake);

            builder.AddCuriosusDefaults();
            using var host = builder.Build();

            host.Services.GetRequiredService<TimeProvider>().Should().BeSameAs(fake);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
            CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _defaultUiCulture;
            _dir.Dispose();
        }

        private sealed class RecordingInitializer(List<string> calls) : IAppInitializer
        {
            public Task InitializeAsync(CancellationToken cancellationToken = default)
            {
                calls.Add("initializer");
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingService(List<string> calls) : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken)
            {
                calls.Add("hosted service");
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
