using System;
using System.IO;
using System.Linq;
using Curiosus.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Web;
using NLog.Targets;
using Xunit;

namespace Curiosus.Hosting.Web.UnitTests
{
    [Collection(NLogCollection.Name)]
    public sealed class AddCuriosusNLogWebTests : IDisposable
    {
        private readonly string _contentRoot =
            Path.Combine(Path.GetTempPath(), "curiosus-web-logging-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void AspNetLayoutRenderers_WorkWithoutRegisteringTheAssemblyInNLogConfig()
        {
            Directory.CreateDirectory(_contentRoot);
            File.WriteAllText(
                Path.Combine(_contentRoot, "NLog.config"),
                """
                <nlog throwExceptions="true" throwConfigExceptions="true">
                  <targets>
                    <target name="mem" type="Memory" layout="${message}|${aspnet-TraceIdentifier}|${aspnet-request-url}" />
                  </targets>
                  <rules>
                    <logger name="*" minlevel="Trace" writeTo="mem" />
                  </rules>
                </nlog>
                """);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = _contentRoot });

            builder.AddCuriosusNLog(configureNLogExtensions: extensions => extensions.RegisterNLogWeb());
            using var app = builder.Build();
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

            LogManager.Configuration!.FindTargetByName<MemoryTarget>("mem")!.Logs
                .Should().Contain("hello||");
        }

        [Fact]
        public void CuriosusWebHost_RegistersAspNetLayoutRenderers()
        {
            Directory.CreateDirectory(_contentRoot);
            File.WriteAllText(
                Path.Combine(_contentRoot, "NLog.config"),
                """
                <nlog throwExceptions="true" throwConfigExceptions="true">
                  <targets>
                    <target name="mem" type="Memory" layout="${message}|${aspnet-TraceIdentifier}" />
                  </targets>
                  <rules>
                    <logger name="*" minlevel="Trace" writeTo="mem" />
                  </rules>
                </nlog>
                """);
            var builder = CuriosusWebHost.CreateBuilder(["--contentRoot", _contentRoot]);

            builder.AddCuriosusNLog();
            using var app = builder.Build();
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("hello");

            LogManager.Configuration!.FindTargetByName<MemoryTarget>("mem")!.Logs.Should().Contain("hello|");
        }

        public void Dispose()
        {
            LogManager.Shutdown();
            LogManager.Configuration = null;
            if (Directory.Exists(_contentRoot))
                Directory.Delete(_contentRoot, recursive: true);
        }
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class NLogCollection
    {
        public const string Name = "NLog global configuration";
    }
}
