using System;
using Microsoft.AspNetCore.Builder;
using NLog;
using NLog.Web;

namespace Curiosus.Hosting.Web
{
    /// <summary>
    /// Creates <see cref="WebApplicationBuilder"/>s with the Curiosus conventions, the web counterpart of
    /// <see cref="CuriosusHost"/>.
    /// </summary>
    public static class CuriosusWebHost
    {
        /// <summary>
        /// Creates a <see cref="WebApplicationBuilder"/>: <c>-c</c> works as <c>--config</c> and the content root of an
        /// app running as a Windows service or a systemd unit is its binaries directory. Chain
        /// <see cref="CuriosusHostingExtensions.AddCuriosusDefaults{TBuilder}"/> to it.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <remarks>
        /// <c>-v</c> / <c>--version</c> prints the app version and exits, as in <see cref="CuriosusHost.CreateBuilder"/>.
        /// The NLog.Web <c>${aspnet-*}</c> layout renderers are registered, so <c>AddCuriosusNLog()</c> needs no
        /// extra setup in a web app.
        /// <see cref="WebApplicationBuilder"/> reads <c>ASPNETCORE_ENVIRONMENT</c> and <c>DOTNET_ENVIRONMENT</c> itself.
        /// </remarks>
        public static WebApplicationBuilder CreateBuilder(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            CuriosusHost.ExitIfVersionRequested(args);

            // only makes ${aspnet-*} known to NLog for a later AddCuriosusNLog; NLog stays off unless the app calls it
            LogManager.Setup().SetupExtensions(extensions => extensions.RegisterNLogWeb());

            return WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = CuriosusHost.MapShortSwitches(args),
                ContentRootPath = CuriosusHost.GetServiceContentRootPath(),
            });
        }
    }
}
