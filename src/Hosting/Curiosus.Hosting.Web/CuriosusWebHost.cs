using System;
using Microsoft.AspNetCore.Builder;

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
        /// <see cref="WebApplicationBuilder"/> reads <c>ASPNETCORE_ENVIRONMENT</c> and <c>DOTNET_ENVIRONMENT</c> itself.
        /// </remarks>
        public static WebApplicationBuilder CreateBuilder(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            return WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = CuriosusHost.MapShortSwitches(args),
                ContentRootPath = CuriosusHost.GetServiceContentRootPath(),
            });
        }
    }
}
