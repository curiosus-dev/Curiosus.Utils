using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Curiosus.Tools.UnitTests;

public class TimeProviderRegistrationTests
{
    [Fact]
    public void AddCuriosusTimeProvider_Nothing_RegistersSystem()
    {
        var services = new ServiceCollection();

        services.AddCuriosusTimeProvider();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(TimeProvider.System);
    }

    [Fact]
    public void AddCuriosusTimeProvider_RegisteredBefore_NotOverridden()
    {
        var services = new ServiceCollection();
        var earlier = new FixedTimeProvider();
        services.AddSingleton<TimeProvider>(earlier);

        services.AddCuriosusTimeProvider();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().Should().BeSameAs(earlier);
        services.Count(d => d.ServiceType == typeof(TimeProvider)).Should().Be(1);
    }

    [Fact]
    public void AddCuriosusTimeProvider_CalledTwice_OneRegistration()
    {
        var services = new ServiceCollection();

        services.AddCuriosusTimeProvider().AddCuriosusTimeProvider();

        services.Count(d => d.ServiceType == typeof(TimeProvider)).Should().Be(1);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
