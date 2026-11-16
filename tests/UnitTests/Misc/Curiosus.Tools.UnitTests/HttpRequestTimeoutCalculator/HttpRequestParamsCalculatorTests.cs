#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
    public void AddHttpRequestTimesCalculator_BindsTheDefaultSection()
    {
        using var provider = BuildProvider(
            s => s.AddHttpRequestTimesCalculator(),
            ("HttpRequestParams:DataRateMBps", "1"),
            ("HttpRequestParams:TimeoutMultiplier", "2"));
        var calculator = provider.GetRequiredService<IHttpRequestParamsCalculator>();

        // 1 GB at 1 MB/s is 1000 s, times 2
        calculator.CalculateTimeout(1_000_000_000).Should().Be(TimeSpan.FromSeconds(2000));
    }

    [Fact]
    public void CustomSection_IsBound()
    {
        using var provider = BuildProvider(
            s => s.AddHttpRequestTimesCalculator("Http:Timeouts"),
            ("Http:Timeouts:DataRateMBps", "3"));

        provider.GetRequiredService<IOptions<HttpRequestParamsCalculatorOptions>>().Value.DataRateMBps.Should().Be(3);
    }

    [Fact]
    public void Configure_OverridesTheBoundValue()
    {
        using var provider = BuildProvider(
            s => s.AddHttpRequestTimesCalculator(configure: o => o.TimeoutMultiplier = 3),
            ("HttpRequestParams:DataRateMBps", "2"),
            ("HttpRequestParams:TimeoutMultiplier", "2"));

        var options = provider.GetRequiredService<IOptions<HttpRequestParamsCalculatorOptions>>().Value;
        options.DataRateMBps.Should().Be(2);
        options.TimeoutMultiplier.Should().Be(3);
    }

    [Fact]
    public void SecondCall_DoesNotBindOrValidateAgain()
    {
        using var provider = BuildProvider(
            s =>
            {
                s.AddHttpRequestTimesCalculator();
                s.AddHttpRequestTimesCalculator("Other", o => o.TimeoutMultiplier = 3);
            },
            ("HttpRequestParams:DataRateMBps", "0"),
            ("Other:DataRateMBps", "5"));

        var act = () => provider.GetRequiredService<IOptions<HttpRequestParamsCalculatorOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle()
            .Which.Should().Contain(nameof(HttpRequestParamsCalculatorOptions.DataRateMBps));
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
        builder.Services.AddHttpRequestTimesCalculator();

        using var host = builder.Build();
        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain(property);
    }

    private static ServiceProvider BuildProvider(
        Action<IServiceCollection> register,
        params (string Key, string Value)[] values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build());
        register(services);
        return services.BuildServiceProvider();
    }
}
