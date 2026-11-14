#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Hosting.UnitTests;

public class AddCuriosusOptionsTests
{
    [Fact]
    public async Task Valid_BoundAndHostStarts()
    {
        using var host = BuildHost(new() { ["Smtp:Host"] = "smtp.local", ["Smtp:Port"] = "25", ["Smtp:Tls:Port"] = "465" },
            s => s.AddCuriosusOptions<SmtpOptions>("Smtp"));

        await host.StartAsync();
        await host.StopAsync();

        var options = host.Services.GetRequiredService<IOptions<SmtpOptions>>().Value;
        options.Host.Should().Be("smtp.local");
        options.Tls.Port.Should().Be(465);
    }

    [Fact]
    public async Task Invalid_HostFailsAtStartWithMember()
    {
        using var host = BuildHost(new() { ["Smtp:Port"] = "25" }, s => s.AddCuriosusOptions<SmtpOptions>("Smtp"));

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain(nameof(SmtpOptions.Host));
    }

    [Fact]
    public async Task InvalidNested_FailsWithNestedPath()
    {
        using var host = BuildHost(new() { ["Smtp:Host"] = "smtp.local", ["Smtp:Tls:Port"] = "0" },
            s => s.AddCuriosusOptions<SmtpOptions>("Smtp"));

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain("SmtpOptions.Tls' members: 'Port'");
    }

    [Fact]
    public async Task Named_EachBoundToItsSection_InvalidOneFailsAtStart()
    {
        var config = new Dictionary<string, string?>
        {
            ["Mail:Primary:Host"] = "primary.local",
            ["Mail:Backup:Host"] = "backup.local",
        };
        using var valid = BuildHost(config, s =>
        {
            s.AddCuriosusOptions<SmtpOptions>("primary", "Mail:Primary");
            s.AddCuriosusOptions<SmtpOptions>("backup", "Mail:Backup");
        });

        await valid.StartAsync();
        await valid.StopAsync();
        var monitor = valid.Services.GetRequiredService<IOptionsMonitor<SmtpOptions>>();
        monitor.Get("primary").Host.Should().Be("primary.local");
        monitor.Get("backup").Host.Should().Be("backup.local");

        using var invalid = BuildHost(config, s => s.AddCuriosusOptions<SmtpOptions>("missing", "Mail:Missing"));
        var act = () => invalid.StartAsync();
        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    private static IHost BuildHost(Dictionary<string, string?> values, System.Action<IServiceCollection> configure)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(values);
        configure(builder.Services);
        return builder.Build();
    }

    public class SmtpOptions
    {
        [Required]
        public string Host { get; set; } = null!;

        [Range(1, 65535)]
        public int Port { get; set; } = 25;

        [ValidateObjectMembers]
        public TlsOptions Tls { get; set; } = new();
    }

    public class TlsOptions
    {
        [Range(1, 65535)]
        public int Port { get; set; } = 465;
    }
}
