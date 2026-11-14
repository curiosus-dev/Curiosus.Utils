using System;
using System.Data.Common;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Curiosus.DateTime.DbSync.UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Default_TimeProviderUntouched()
    {
        var services = CreateServices();
        services.AddSingleton<IDbClock>(new StubDbClock());
        services.TryAddSingleton(TimeProvider.System);
        services.AddCuriosusDbSyncTimeProvider();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
        provider.GetRequiredService<DbSyncTimeProvider>().Should().NotBeNull();
        provider.GetServices<IHostedService>().Should().ContainSingle(s => s is DbTimeSyncService);
    }

    [Fact]
    public void RegisterAsTimeProvider_RegisteredBefore_Replaced()
    {
        var services = CreateServices();
        services.TryAddSingleton(TimeProvider.System);
        services.AddCuriosusDbSyncTimeProvider(registerAsTimeProvider: true);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(provider.GetRequiredService<DbSyncTimeProvider>());
    }

    [Fact]
    public void RegisterAsTimeProvider_TryAddAfter_StillDbSync()
    {
        var services = CreateServices();
        services.AddCuriosusDbSyncTimeProvider(registerAsTimeProvider: true);
        services.TryAddSingleton(TimeProvider.System);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(provider.GetRequiredService<DbSyncTimeProvider>());
        services.Count(d => d.ServiceType == typeof(TimeProvider)).Should().Be(1);
    }

    [Fact]
    public void DefaultClock_PostgresOverDefaultDataSource()
    {
        var services = CreateServices();
        services.AddSingleton<DbDataSource>(NpgsqlDataSource.Create("Host=localhost"));
        services.AddCuriosusDbSyncTimeProvider();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDbClock>().Should().BeOfType<PostgresDbClock>();
    }

    [Fact]
    public void DefaultClock_KeyedDataSource_Resolved()
    {
        var services = CreateServices();
        services.AddKeyedSingleton<DbDataSource>("primary", NpgsqlDataSource.Create("Host=localhost"));
        services.AddCuriosusDbSyncTimeProvider(dataSourceKey: "primary");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDbClock>().Should().BeOfType<PostgresDbClock>();
    }

    [Fact]
    public void CustomClockRegisteredBefore_Kept()
    {
        var services = CreateServices();
        var clock = new StubDbClock();
        services.AddSingleton<IDbClock>(clock);
        services.AddCuriosusDbSyncTimeProvider();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDbClock>().Should().BeSameAs(clock);
    }

    [Fact]
    public void CalledTwice_OneHostedServiceAndValidator()
    {
        var services = CreateServices();
        services.AddCuriosusDbSyncTimeProvider();
        services.AddCuriosusDbSyncTimeProvider();

        services.Count(d => d.ImplementationType == typeof(DbTimeSyncService)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IValidateOptions<DbSyncTimeOptions>)).Should().Be(1);
    }

    [Theory]
    [InlineData(nameof(DbSyncTimeOptions.SyncPeriod))]
    [InlineData(nameof(DbSyncTimeOptions.RetryPeriod))]
    [InlineData(nameof(DbSyncTimeOptions.MaxAcceptedRtt))]
    public void InvalidOptions_FailValidation(string property)
    {
        var services = CreateServices();
        services.AddCuriosusDbSyncTimeProvider(o => typeof(DbSyncTimeOptions).GetProperty(property)!.SetValue(o, TimeSpan.Zero));

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IOptions<DbSyncTimeOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain(property);
    }

    [Fact]
    public void NegativeInitialTimeout_FailsValidation_ZeroAllowed()
    {
        new DbSyncTimeOptionsValidator().Validate(null, new DbSyncTimeOptions { InitialSyncTimeout = TimeSpan.FromSeconds(-1) })
            .Failed.Should().BeTrue();
        new DbSyncTimeOptionsValidator().Validate(null, new DbSyncTimeOptions { InitialSyncTimeout = TimeSpan.Zero })
            .Succeeded.Should().BeTrue();
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        return services;
    }

    private sealed class StubDbClock : IDbClock
    {
        public System.Threading.Tasks.Task<DateTimeOffset> GetUtcNowAsync(System.Threading.CancellationToken cancellationToken)
            => System.Threading.Tasks.Task.FromResult(DateTimeOffset.UtcNow);
    }
}
