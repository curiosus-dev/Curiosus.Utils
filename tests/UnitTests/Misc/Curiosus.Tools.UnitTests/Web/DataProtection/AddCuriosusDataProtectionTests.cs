using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Curiosus.Tools.Web.DataProtection;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web.DataProtection;

public sealed class AddCuriosusDataProtectionTests : IDisposable
{
    private readonly string _keyRing = Path.Combine(Path.GetTempPath(), "curiosus-keys-" + Guid.NewGuid().ToString("N"));

    public AddCuriosusDataProtectionTests()
    {
        Directory.CreateDirectory(_keyRing);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>("DataProtection:" + v.Key, v.Value)))
            .Build();

    private static ServiceCollection Services(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        return services;
    }

    public static TheoryData<string, bool> Orders => new()
    {
        { "none", false }, { "none", true },
        { "authentication-before", false }, { "authentication-before", true },
        { "authentication-after", false }, { "authentication-after", true },
        { "mvc-before", false }, { "mvc-before", true },
    };

    [Theory]
    [MemberData(nameof(Orders))]
    public void Data_IsAlwaysProtected_WhateverTheOrderOfRegistrations(string order, bool withSection)
    {
        var services = Services(
            withSection ? Configuration(("ApplicationName", "app"), ("KeyRingPath", _keyRing)) : Configuration());
        if (order == "authentication-before") services.AddAuthentication().AddCookie();
        if (order == "mvc-before") services.AddControllersWithViews();
        services.AddCuriosusDataProtection();
        if (order == "authentication-after") services.AddAuthentication().AddCookie();

        using var provider = services.BuildServiceProvider();
        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("tests");

        var protectedPayload = protector.Protect("role=admin");
        protectedPayload.Should().NotContain("role=admin");
        protector.Unprotect(protectedPayload).Should().Be("role=admin");

        var tampered = protectedPayload.Substring(0, protectedPayload.Length - 4) + "AAAA";
        var act = () => protector.Unprotect(tampered);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void KeysAreWrittenToTheKeyRingPath()
    {
        var services = Services(Configuration(("KeyRingPath", _keyRing)));
        services.AddCuriosusDataProtection();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("tests").Protect("x");

        Directory.GetFiles(_keyRing, "key-*.xml").Should().NotBeEmpty();
    }

    [Fact]
    public void KeyRing_IsSharedByTheSameApplicationName_Only()
    {
        string payload;
        using (var first = Build("app"))
            payload = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("tests").Protect("secret");

        using (var second = Build("app"))
            second.GetRequiredService<IDataProtectionProvider>().CreateProtector("tests").Unprotect(payload).Should().Be("secret");

        using var other = Build("other-app");
        var act = () => other.GetRequiredService<IDataProtectionProvider>().CreateProtector("tests").Unprotect(payload);
        act.Should().Throw<CryptographicException>();

        ServiceProvider Build(string applicationName)
        {
            var services = Services(Configuration(("ApplicationName", applicationName), ("KeyRingPath", _keyRing)));
            services.AddCuriosusDataProtection();
            return services.BuildServiceProvider();
        }
    }

    [Theory]
    [InlineData("missing-dir", false)]
    [InlineData(null, true)]
    public async Task InvalidOptions_FailTheStart(string? keyRing, bool disableGeneration)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        var values = new List<(string, string)>();
        if (keyRing != null) values.Add(("KeyRingPath", Path.Combine(_keyRing, keyRing)));
        if (disableGeneration) values.Add(("DisableAutomaticKeyGeneration", "true"));
        builder.Configuration.AddConfiguration(Configuration(values.ToArray()));
        builder.Services.AddCuriosusDataProtection();

        using var host = builder.Build();
        var act = () => host.StartAsync();

        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_keyRing))
            Directory.Delete(_keyRing, recursive: true);
    }
}
