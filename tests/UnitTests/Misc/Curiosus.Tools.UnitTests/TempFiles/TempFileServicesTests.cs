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
    public async Task AddTempFileServices_BindsOptions_AndCreatesStreamsInTheTempDirectory()
    {
        var builder = CreateBuilder(("TempFiles:TempPath", _tempPath));
        builder.Services.AddTempFileServices().BindConfiguration("TempFiles");

        using var host = builder.Build();
        await host.StartAsync();

        foreach (var initializer in host.Services.GetServices<IAppInitializer>())
            await initializer.InitializeAsync();
        Directory.Exists(_tempPath).Should().BeTrue();

        using var stream = host.Services.GetRequiredService<ITempFileStreamFactory>().CreateTempFileStream("a.tmp");
        stream.FullPath.Should().Be(Path.GetFullPath(Path.Combine(_tempPath, "a.tmp")));

        await host.StopAsync();
    }

    [Theory]
    [InlineData("TempPath", " ")]
    [InlineData("CleaningFrequencyHours", "0")]
    [InlineData("FileTtlHours", "0")]
    public async Task InvalidOptions_FailTheStart(string property, string value)
    {
        var builder = CreateBuilder(($"TempFiles:{property}", value));
        builder.Services.AddTempFileServices().BindConfiguration("TempFiles");

        using var host = builder.Build();
        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain(property);
    }

    [Fact]
    public void SeveralRegistrations_ValidateOnce_AndAddOneInitializer()
    {
        var services = new ServiceCollection();
        services.AddTempFileServices(o => o.FileTtlHours = 0);
        services.AddTempFileServices();
        services.AddTempFileOptions();

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IOptions<TempFileOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle();
        services.Count(d => d.ServiceType == typeof(IAppInitializer)).Should().Be(1);
    }

    [Fact]
    public void Configure_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddTempFileServices(o => o.TempPath = _tempPath);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<TempFileOptions>>().Value.TempPath.Should().Be(_tempPath);
    }

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
