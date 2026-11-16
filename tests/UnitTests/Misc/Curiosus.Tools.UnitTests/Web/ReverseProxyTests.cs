#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Curiosus.Tools.Web.ReverseProxy;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web;

public sealed class ReverseProxyTests
{
    private static ServiceCollection Services(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        return services;
    }

    [Fact]
    public void AddReverseProxy_ConfiguresForwardedHeadersFromTheDefaultSection_Once()
    {
        var services = Services(("ReverseProxy:ProxyIps:0", "10.0.0.1"), ("ReverseProxy:ProxyIps:1", " "));
        services.AddReverseProxy();
        services.AddReverseProxy();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.KnownProxies.Count(ip => ip.Equals(IPAddress.Parse("10.0.0.1"))).Should().Be(1);
    }

    [Fact]
    public void AddReverseProxy_BindsACustomSection_ThenConfigureOverrides()
    {
        var services = Services(("Web:Proxy:PathBase", "/api"), ("Web:Proxy:ProxyIps:0", "10.0.0.1"));
        services.AddReverseProxy("Web:Proxy", o => o.PathBase = "/app");
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;

        options.PathBase.Should().Be("/app");
        options.ProxyIps.Should().Equal("10.0.0.1");
    }

    [Fact]
    public async Task UseReverseProxy_AppliesForwardedHeadersAndPathBase()
    {
        var services = Services(("ReverseProxy:PathBase", "/api"), ("ReverseProxy:ProxyIps:0", "10.0.0.1"));
        services.AddLogging();
        services.AddReverseProxy();
        using var provider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        app.UseReverseProxy();
        // the middleware restores the request when the pipeline returns: capture the values inside it
        (PathString PathBase, PathString Path, string Scheme, IPAddress? RemoteIp) seen = default;
        app.Run(context =>
        {
            seen = (context.Request.PathBase, context.Request.Path, context.Request.Scheme,
                context.Connection.RemoteIpAddress);
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Path = "/api/orders";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.7";
        await app.Build()(context);

        seen.PathBase.Should().Be(new PathString("/api"));
        seen.Path.Should().Be(new PathString("/orders"));
        seen.Scheme.Should().Be("https");
        seen.RemoteIp.Should().Be(IPAddress.Parse("203.0.113.7"));
    }

    [Fact]
    public void UseReverseProxy_WithoutAddReverseProxy_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var act = () => new ApplicationBuilder(provider).UseReverseProxy();

        act.Should().Throw<InvalidOperationException>().WithMessage("*AddReverseProxy*");
    }

    [Theory]
    [InlineData("api", null, "PathBase")]
    [InlineData(null, "not-an-ip", "ProxyIps[1]")]
    public async Task InvalidOptions_FailTheStart(string? pathBase, string? proxyIp, string failedOption)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddReverseProxy(configure: o =>
        {
            o.PathBase = pathBase;
            if (proxyIp != null) o.ProxyIps = ["10.0.0.1", proxyIp];
        });
        builder.Services.AddReverseProxy();   // a second call doesn't report the errors twice
        using var host = builder.Build();

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().ContainSingle().Which.Should().Contain(failedOption);
    }
}
