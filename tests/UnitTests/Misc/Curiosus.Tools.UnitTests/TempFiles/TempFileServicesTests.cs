#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Curiosus.Tools.AppInitializer;
using Curiosus.Tools.TempFiles;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Tools.UnitTests.TempFiles;

public sealed class TempFileServicesTests : IDisposable
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), "curiosus-tools-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AddTempFileServices_BindsTheDefaultSection_AndCreatesStreamsInTheTempDirectory()
    {
        var builder = CreateBuilder(("TempFiles:TempPath", _tempPath));
        builder.Services.AddTempFileServices();

        using var host = builder.Build();
        await host.StartAsync();

        foreach (var initializer in host.Services.GetServices<IAppInitializer>())
            await initializer.InitializeAsync();
        Directory.Exists(_tempPath).Should().BeTrue();

        using var stream = host.Services.GetRequiredService<ITempFileStreamFactory>().CreateTempFileStream("a.tmp");
        stream.FullPath.Should().Be(Path.GetFullPath(Path.Combine(_tempPath, "a.tmp")));

        await host.StopAsync();
    }

    [Fact]
    public void CustomSection_IsBound()
    {
        using var provider = BuildProvider(
            s => s.AddTempFileServices("App:Temp"),
            ("App:Temp:TempPath", _tempPath),
            ("TempFiles:TempPath", "other"));

        provider.GetRequiredService<IOptions<TempFileOptions>>().Value.TempPath.Should().Be(_tempPath);
    }

    [Fact]
    public void Configure_OverridesTheBoundValue()
    {
        using var provider = BuildProvider(
            s => s.AddTempFileServices(configure: o => o.FileTtlHours = 5),
            ("TempFiles:TempPath", _tempPath),
            ("TempFiles:FileTtlHours", "48"));

        var options = provider.GetRequiredService<IOptions<TempFileOptions>>().Value;
        options.TempPath.Should().Be(_tempPath);
        options.FileTtlHours.Should().Be(5);
    }

    [Fact]
    public void MissingSection_KeepsTheDefaults()
    {
        using var provider = BuildProvider(s => s.AddTempFileServices());

        provider.GetRequiredService<IOptions<TempFileOptions>>().Value.TempPath.Should().Be("./.tmp");
    }

    [Theory]
    [InlineData("TempPath", " ")]
    [InlineData("CleaningFrequencyHours", "0")]
    [InlineData("FileTtlHours", "0")]
    public async Task InvalidOptions_FailTheStart(string property, string value)
    {
        var builder = CreateBuilder(($"TempFiles:{property}", value));
        builder.Services.AddTempFileServices();

        using var host = builder.Build();
        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain(property);
    }

    [Fact]
    public void SeveralRegistrations_BindAndValidateOnce_AndAddOneInitializer()
    {
        var services = new ServiceCollection();
        services.AddTempFileServices();
        services.AddTempFileServices();
        services.AddTempFileOptions();

        services.Count(d => d.ServiceType == typeof(IConfigureOptions<TempFileOptions>)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IValidateOptions<TempFileOptions>)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IAppInitializer)).Should().Be(1);

        services.AddSingleton<IConfiguration>(Configuration(("TempFiles:FileTtlHours", "0")));
        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IOptions<TempFileOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle();
    }

    [Fact]
    public void SecondRegistration_AddsOnlyItsConfigure()
    {
        using var provider = BuildProvider(
            s =>
            {
                s.AddTempFileServices();
                s.AddTempFileOptions("Other", o => o.FileTtlHours = 5);
            },
            ("TempFiles:TempPath", _tempPath),
            ("Other:TempPath", "ignored"));

        var options = provider.GetRequiredService<IOptions<TempFileOptions>>().Value;
        options.TempPath.Should().Be(_tempPath);
        options.FileTtlHours.Should().Be(5);
    }

    private static ServiceProvider BuildProvider(
        Action<IServiceCollection> register,
        params (string Key, string Value)[] values)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(Configuration(values));
        register(services);
        return services.BuildServiceProvider();
    }

    private static IConfigurationRoot Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build();

    private static HostApplicationBuilder CreateBuilder(params (string Key, string Value)[] values)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)));
        return builder;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }
}
