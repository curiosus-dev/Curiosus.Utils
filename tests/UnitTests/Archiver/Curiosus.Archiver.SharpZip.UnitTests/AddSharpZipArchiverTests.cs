using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Curiosus.Tools.TempFiles;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Archiver.SharpZip.UnitTests;

public sealed class AddSharpZipArchiverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "curiosus-sharpzip-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ZipDirAsync_WritesTheArchiveToTheConfiguredTempPath()
    {
        var tempPath = Path.Combine(_root, "tmp");
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "a.txt"), "hello");

        var builder = CreateBuilder(("TempFiles:TempPath", tempPath));
        builder.Services.AddSharpZipArchiver();
        using var host = builder.Build();
        await host.StartAsync();

        var archiver = host.Services.GetRequiredService<IArchiver>();
        await using (var zip = await archiver.ZipDirAsync(source))
        {
            Path.GetDirectoryName(zip.FullPath).Should().Be(Path.GetFullPath(tempPath));
            using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
            archive.Entries.Select(e => e.FullName).Should().Equal("a.txt");
        }

        await host.StopAsync();
    }

    [Theory]
    [InlineData("TempPath", "")]
    [InlineData("FileTtlHours", "0")]
    public async Task InvalidOptions_FailTheStart(string property, string value)
    {
        var builder = CreateBuilder(($"TempFiles:{property}", value));
        builder.Services.AddSharpZipArchiver();

        using var host = builder.Build();
        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain(property);
    }

    [Fact]
    public void WithTempFileServices_ReportsErrorsOnce()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TempFiles:FileTtlHours"] = "0" })
            .Build());
        services.AddSharpZipArchiver();
        services.AddTempFileServices();

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IOptions<TempFileOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle();
    }

    [Fact]
    public void CustomSection_IsBound_AndConfigureOverridesIt()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Zip:Temp:TempPath"] = "/data/zip",
                ["Zip:Temp:FileTtlHours"] = "48"
            })
            .Build());
        services.AddSharpZipArchiver("Zip:Temp", o => o.FileTtlHours = 5);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TempFileOptions>>().Value;

        options.TempPath.Should().Be("/data/zip");
        options.FileTtlHours.Should().Be(5);
    }

    private static HostApplicationBuilder CreateBuilder(params (string Key, string Value)[] values)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)));
        return builder;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
