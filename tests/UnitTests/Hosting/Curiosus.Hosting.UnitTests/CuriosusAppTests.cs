using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Configuration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Curiosus.Hosting.UnitTests
{
    public sealed class CuriosusAppTests
    {
        private static HostApplicationBuilder CreateBuilder() =>
            Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        [Fact]
        public async Task Command_ReturnsItsCode()
        {
            var code = await CuriosusApp.RunAsync(CreateBuilder().Build(), (_, _) => Task.FromResult(5));

            code.Should().Be(5);
        }

        [Fact]
        public async Task Command_GetsServicesAndTheHostIsDisposed()
        {
            var builder = CreateBuilder();
            builder.Services.AddSingleton<Tracker>();
            Tracker? tracker = null;

            var code = await CuriosusApp.RunAsync(builder.Build(), (services, _) =>
            {
                tracker = services.GetRequiredService<Tracker>();
                return Task.FromResult(CuriosusExitCodes.Success);
            });

            code.Should().Be(CuriosusExitCodes.Success);
            tracker!.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public async Task InvalidOptions_ReturnInvalidOptions()
        {
            var builder = CreateBuilder();
            builder.Services.AddCuriosusOptions<RequiredOptions>("Missing");

            var code = await CuriosusApp.RunAsync(builder.Build(), (_, _) => Task.FromResult(0));

            code.Should().Be(CuriosusExitCodes.InvalidOptions);
        }

        [Fact]
        public async Task SeveralInvalidOptions_ReturnInvalidOptions()
        {
            var builder = CreateBuilder();
            builder.Services.AddCuriosusOptions<RequiredOptions>("Missing");
            builder.Services.AddCuriosusOptions<OtherRequiredOptions>("AlsoMissing");

            var code = await CuriosusApp.RunAsync(builder.Build(), (_, _) => Task.FromResult(0));

            code.Should().Be(CuriosusExitCodes.InvalidOptions);
        }

        [Fact]
        public async Task OperationCanceledWithoutCancellation_ReturnsUnhandledException()
        {
            var code = await CuriosusApp.RunAsync(
                CreateBuilder().Build(),
                (_, _) => throw new TaskCanceledException("HttpClient timeout"));

            code.Should().Be(CuriosusExitCodes.UnhandledException);
        }

        [Fact]
        public async Task FailedCommand_StillStopsTheHost()
        {
            var builder = CreateBuilder();
            builder.Services.AddSingleton<StopTracker>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<StopTracker>());
            StopTracker? tracker = null;

            var code = await CuriosusApp.RunAsync(builder.Build(), (services, _) =>
            {
                tracker = services.GetRequiredService<StopTracker>();
                throw new InvalidOperationException("boom");
            });

            code.Should().Be(CuriosusExitCodes.UnhandledException);
            tracker!.IsStopped.Should().BeTrue();
        }

        [Fact]
        public async Task FailedBackgroundService_ReturnsUnhandledException()
        {
            var builder = CreateBuilder();
            builder.Services.AddHostedService<FailAtOnce>();

            var code = await CuriosusApp.RunAsync(builder.Build());

            code.Should().Be(CuriosusExitCodes.UnhandledException);
        }

        [Fact]
        public async Task UnhandledException_ReturnsUnhandledException()
        {
            var code = await CuriosusApp.RunAsync(
                CreateBuilder().Build(),
                (_, _) => throw new InvalidOperationException("boom"));

            code.Should().Be(CuriosusExitCodes.UnhandledException);
        }

        [Fact]
        public async Task Cancellation_ReturnsCancellation()
        {
            using var cts = new CancellationTokenSource();

            var code = await CuriosusApp.RunAsync(CreateBuilder().Build(), async (_, ct) =>
            {
                await cts.CancelAsync();
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            }, cts.Token);

            code.Should().Be(CuriosusExitCodes.Cancellation);
        }

        [Fact]
        public async Task ServiceStoppedByItself_ReturnsSuccess()
        {
            var builder = CreateBuilder();
            builder.Services.AddHostedService<StopAtOnce>();

            var code = await CuriosusApp.RunAsync(builder.Build());

            code.Should().Be(CuriosusExitCodes.Success);
        }

        private sealed class Tracker : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose() => IsDisposed = true;
        }

        private sealed class RequiredOptions
        {
            [Required]
            public string? Name { get; set; }
        }

        private sealed class OtherRequiredOptions
        {
            [Required]
            public string? Name { get; set; }
        }

        private sealed class StopTracker : IHostedService
        {
            public bool IsStopped { get; private set; }

            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            public Task StopAsync(CancellationToken cancellationToken)
            {
                IsStopped = true;
                return Task.CompletedTask;
            }
        }

        private sealed class FailAtOnce : BackgroundService
        {
            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                await Task.Yield();
                throw new InvalidOperationException("worker failed");
            }
        }

        private sealed class StopAtOnce(IHostApplicationLifetime lifetime) : BackgroundService
        {
            protected override Task ExecuteAsync(CancellationToken stoppingToken)
            {
                lifetime.StopApplication();
                return Task.CompletedTask;
            }
        }
    }
}
