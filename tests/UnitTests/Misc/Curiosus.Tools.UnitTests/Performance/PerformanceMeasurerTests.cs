using System;
using System.Linq;
using System.Threading;
using Curiosus.Tools.Performance;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Curiosus.Tools.UnitTests.Performance;

public sealed class PerformanceMeasurerTests
{
    private readonly FakeLogCollector _collector = new();
    private readonly PerformanceMeasurer _measurer;

    public PerformanceMeasurerTests()
    {
        _measurer = new PerformanceMeasurer(new FakeLoggerFactory(_collector));
    }

    [Fact]
    public void Dispose_LogsNameAndWholeMillisecondsToThePerformanceLogger()
    {
        using (_measurer.Measure("Import"))
        {
            Thread.Sleep(20);
        }

        var record = _collector.GetSnapshot().Should().ContainSingle().Subject;
        record.Category.Should().Be("performance");
        record.Level.Should().Be(LogLevel.Information);
        record.Message.Should().MatchRegex(@"^Import -> \d+ ms$");

        var state = record.StructuredState!.ToDictionary(p => p.Key, p => p.Value);
        state["{OriginalFormat}"].Should().Be("{MeasurerName} -> {MeasurerSpentTime} ms");
        state["MeasurerName"].Should().Be("Import");
        long.Parse(state["MeasurerSpentTime"]!).Should().BeGreaterThanOrEqualTo(15);
    }

    [Fact]
    public void ReplaceName_ChangesTheLoggedName()
    {
        using (var scope = _measurer.Measure("Import"))
        {
            scope.ReplaceName("Import 42");
        }

        _collector.LatestRecord.Message.Should().StartWith("Import 42 -> ");
    }

    [Fact]
    public void SecondDispose_DoesNotLogAgain()
    {
        var scope = _measurer.Measure("Import");

        scope.Dispose();
        scope.Dispose();

        _collector.Count.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Measure_EmptyName_Throws(string name)
    {
        var act = () => _measurer.Measure(name);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddCuriosusPerformanceMeasurer_RegistersSingleton()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCuriosusPerformanceMeasurer()
            .AddCuriosusPerformanceMeasurer()
            .BuildServiceProvider();

        provider.GetRequiredService<PerformanceMeasurer>()
            .Should().BeSameAs(provider.GetRequiredService<PerformanceMeasurer>());
    }

    private sealed class FakeLoggerFactory(FakeLogCollector collector) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new FakeLogger(collector, categoryName);

        public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
