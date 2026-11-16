#nullable enable

using System.Collections.Generic;
using System.Linq;
using Curiosus.Tools.Web.Controllers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web.Controllers;

public sealed class AddRedirectAllowListTests
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
    public void BindsTheDefaultSection()
    {
        var services = Services(("Redirects:AllowedHosts:0", "app.example.com"));

        services.AddRedirectAllowList();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<RedirectAllowListOptions>>().Value.AllowedHosts
            .Should().Equal("app.example.com");
    }

    [Fact]
    public void BindsACustomSection_ThenConfigureAdds_OnceForTwoCalls()
    {
        var services = Services(("Web:Redirects:AllowedHosts:0", "app.example.com"));

        services.AddRedirectAllowList("Web:Redirects", o => o.AllowedHosts.Add("www.example.com"));
        services.AddRedirectAllowList("Web:Redirects");
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<RedirectAllowListOptions>>().Value.AllowedHosts
            .Should().Equal("app.example.com", "www.example.com");
    }
}
