using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
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
    public void Registered_TimeProviderIsDbSync()
    {
        var services = CreateServices();
        services.AddSingleton<IDbClock>(new StubDbClock());
        services.AddCuriosusDbSyncTimeProvider();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(provider.GetRequiredService<DbSyncTimeProvider>());
        provider.GetServices<IHostedService>().Should().ContainSingle(s => s is DbTimeSyncService);
    }

    [Fact]
    public void SystemRegisteredBefore_Replaced()
    {
        var services = CreateServices();
        services.AddSingleton(TimeProvider.System);
        services.AddCuriosusDbSyncTimeProvider();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(provider.GetRequiredService<DbSyncTimeProvider>());
    }

    [Fact]
    public void SystemTryAddedAfter_StillDbSync()
    {
        var services = CreateServices();
        services.AddCuriosusDbSyncTimeProvider();
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
        services.AddCuriosusDbSyncTimeProvider(
            configure: o => typeof(DbSyncTimeOptions).GetProperty(property)!.SetValue(o, TimeSpan.Zero));

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

    [Fact]
    public void Binds_TheDefaultSection()
    {
        DbSyncTimeOptions.SectionName.Should().Be("DbTimeSync");
        var services = CreateServices(Section);
        services.AddCuriosusDbSyncTimeProvider();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DbSyncTimeOptions>>().Value;

        options.SyncPeriod.Should().Be(TimeSpan.FromMinutes(5));
        options.RetryPeriod.Should().Be(TimeSpan.FromSeconds(3));
        options.InitialSyncTimeout.Should().Be(TimeSpan.FromSeconds(5), "a key missing in the section keeps the default");
    }

    [Fact]
    public void Binds_ACustomSectionPath()
    {
        var services = CreateServices(Section.ToDictionary(p => "Clock:" + p.Key, p => p.Value));
        services.AddCuriosusDbSyncTimeProvider("Clock:DbTimeSync");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<DbSyncTimeOptions>>().Value.SyncPeriod.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void MissingSection_DefaultsApply()
    {
        var services = CreateServices();
        services.AddCuriosusDbSyncTimeProvider();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<DbSyncTimeOptions>>().Value.SyncPeriod.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Configure_OverridesTheBoundValues()
    {
        var services = CreateServices(Section);
        services.AddCuriosusDbSyncTimeProvider(configure: o => o.SyncPeriod = TimeSpan.FromHours(1));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DbSyncTimeOptions>>().Value;

        options.SyncPeriod.Should().Be(TimeSpan.FromHours(1));
        options.RetryPeriod.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void CalledTwice_SecondConfigureApplies_SectionNotBoundAgain()
    {
        var services = CreateServices(Section);
        services.AddCuriosusDbSyncTimeProvider(configure: o => o.SyncPeriod = TimeSpan.FromHours(1));
        services.AddCuriosusDbSyncTimeProvider("Other", o => o.RetryPeriod = TimeSpan.FromSeconds(1));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DbSyncTimeOptions>>().Value;

        options.SyncPeriod.Should().Be(TimeSpan.FromHours(1), "the section is not bound again over the first configure");
        options.RetryPeriod.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CalledTwice_ReportsEveryErrorOnce()
    {
        var services = CreateServices();
        services.AddCuriosusDbSyncTimeProvider();
        services.AddCuriosusDbSyncTimeProvider(configure: o => o.SyncPeriod = TimeSpan.Zero);

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IOptions<DbSyncTimeOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SectionPath_CantBeEmpty(string path)
    {
        var act = () => CreateServices().AddCuriosusDbSyncTimeProvider(path);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task InvalidSection_FailsTheHostStart()
    {
        using var host = new HostBuilder()
            .ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DbTimeSync:SyncPeriod"] = "00:00:00",
            }))
            .ConfigureServices(services =>
            {
                services.AddSingleton<IDbClock>(new StubDbClock());
                services.AddCuriosusDbSyncTimeProvider();
            })
            .Build();

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain(nameof(DbSyncTimeOptions.SyncPeriod));
    }

    private static readonly Dictionary<string, string?> Section = new()
    {
        ["DbTimeSync:SyncPeriod"] = "00:05:00",
        ["DbTimeSync:RetryPeriod"] = "00:00:03",
    };

    private static ServiceCollection CreateServices(Dictionary<string, string?>? configuration = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(configuration ?? [])
            .Build());
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
