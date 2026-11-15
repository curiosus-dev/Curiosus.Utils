using System;
using System.Diagnostics;
using System.Globalization;
using Curiosus.Tools.Performance;
using FluentAssertions;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Curiosus.Tools.UnitTests.Performance;

public sealed class StuckCodeWatcherTests
{
    private static long Later(TimeSpan delay) => Stopwatch.GetTimestamp() + (long)(delay.TotalSeconds * Stopwatch.Frequency);

    [Fact]
    public void SameLabelEnteredTwice_IsTrackedSeparately()
    {
        var watcher = new StuckCodeWatcher();
        var logger = new FakeLogger();

        var first = watcher.Enter("import", 1);
        using var second = watcher.Enter("import", 1);
        watcher.Count.Should().Be(2);

        first.Dispose();
        watcher.Count.Should().Be(1);

        watcher.Validate(logger, Later(TimeSpan.FromSeconds(2)));
        logger.Collector.Count.Should().Be(1);
    }

    [Fact]
    public void CodeWithinTimeout_IsNotLogged()
    {
        var watcher = new StuckCodeWatcher();
        var logger = new FakeLogger();
        using var _ = watcher.Enter("import", 60);

        watcher.Validate(logger, Later(TimeSpan.FromSeconds(2)));

        logger.Collector.Count.Should().Be(0);
    }

    [Fact]
    public void LogOutput_DoesNotDependOnCurrentCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
        try
        {
            var watcher = new StuckCodeWatcher();
            var logger = new FakeLogger();
            using var _ = watcher.Enter("import", 1);

            watcher.Validate(logger, Later(TimeSpan.FromSeconds(75)));

            logger.LatestRecord.Message.Should().MatchRegex(
                @"^STUCK CODE: ""import"", duration 01:1\d \(entered at \d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z from thread #\d+\)$");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
