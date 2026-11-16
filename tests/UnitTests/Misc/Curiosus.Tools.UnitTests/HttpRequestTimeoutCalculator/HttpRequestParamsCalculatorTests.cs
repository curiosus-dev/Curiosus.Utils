using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Curiosus.Tools.HttpRequestTimeoutCalculator;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Tools.UnitTests.HttpRequestTimeoutCalculator;

public sealed class HttpRequestParamsCalculatorTests
{
    [Fact]
    public void AddHttpRequestTimesCalculator_UsesConfiguredOptions()
    {
        var services = new ServiceCollection();
        services.AddHttpRequestTimesCalculator(o =>
        {
            o.DataRateMBps = 1;
            o.TimeoutMultiplier = 2;
        });

        using var provider = services.BuildServiceProvider();
        var calculator = provider.GetRequiredService<IHttpRequestParamsCalculator>();

        // 1 GB at 1 MB/s is 1000 s, times 2
        calculator.CalculateTimeout(1_000_000_000).Should().Be(TimeSpan.FromSeconds(2000));
    }

    [Theory]
    [InlineData("DataRateMBps", "0")]
    [InlineData("DataRateMBps", "-1")]
    [InlineData("TimeoutMultiplier", "0")]
    [InlineData("TimeoutMultiplier", "-0.5")]
    public async Task InvalidOptions_FailTheStart(string property, string value)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"HttpRequestParams:{property}"] = value
        });
        builder.Services.AddHttpRequestTimesCalculator().BindConfiguration("HttpRequestParams");

        using var host = builder.Build();
        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain(property);
    }
}
