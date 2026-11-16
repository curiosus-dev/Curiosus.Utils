#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Curiosus.Tools.Web.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web.Middleware;

public sealed class PreviewDetectorTests
{
    private const string PreviewBot = "WhatsApp/2.19.333 A";

    private static async Task<PathString> SendAsync(Action<IApplicationBuilder> use, IServiceProvider services, string userAgent)
    {
        var app = new ApplicationBuilder(services);
        use(app);
        PathString seenPath = default;
        app.Run(context =>
        {
            seenPath = context.Request.Path;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/articles/5";
        context.Request.Headers.UserAgent = userAgent;
        await app.Build()(context);

        return seenPath;
    }

    [Theory]
    [InlineData(PreviewBot, "/preview")]
    [InlineData("Mozilla/5.0", "/articles/5")]
    public async Task UsePreviewDetector_RewritesThePathOfPreviewRequests(string userAgent, string expectedPath)
    {
        var services = new ServiceCollection();
        services.AddPreviewDetector(o => o.RedirectPath = "/preview");
        using var provider = services.BuildServiceProvider();

        var path = await SendAsync(app => app.UsePreviewDetector(), provider, userAgent);

        path.Should().Be(new PathString(expectedPath));
    }

    [Fact]
    public async Task UsePreviewDetector_WithPath_NeedsNoRegistration()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var path = await SendAsync(app => app.UsePreviewDetector("/preview"), provider, PreviewBot);

        path.Should().Be(new PathString("/preview"));
    }

    [Fact]
    public void UsePreviewDetector_WithoutAddPreviewDetector_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var act = () => new ApplicationBuilder(provider).UsePreviewDetector();

        act.Should().Throw<InvalidOperationException>().WithMessage("*AddPreviewDetector*");
    }

    [Fact]
    public void AddPreviewDetector_BindsConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PreviewDetect:RedirectPath"] = "/preview" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddPreviewDetector().BindConfiguration("PreviewDetect");
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<PreviewDetectOptions>>().Value.RedirectPath
            .Should().Be(new PathString("/preview"));
    }

    [Fact]
    public async Task EmptyRedirectPath_FailsTheStart_Once()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddPreviewDetector();
        builder.Services.AddPreviewDetector();
        using var host = builder.Build();

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().ContainSingle().Which.Should().Contain(nameof(PreviewDetectOptions.RedirectPath));
    }
}
