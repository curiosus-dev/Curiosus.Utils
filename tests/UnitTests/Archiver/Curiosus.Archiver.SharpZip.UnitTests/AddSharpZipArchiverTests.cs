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
        builder.Services.AddSharpZipArchiver().BindConfiguration("TempFiles");
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
        builder.Services.AddSharpZipArchiver().BindConfiguration("TempFiles");

        using var host = builder.Build();
        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain(property);
    }

    [Fact]
    public void Configure_IsApplied()
    {
        var services = new ServiceCollection();
        services.AddSharpZipArchiver(o => o.TempPath = _root);

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<TempFileOptions>>().Value.TempPath.Should().Be(_root);
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
