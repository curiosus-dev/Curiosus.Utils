using System;
using Curiosus.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;
using NLog.Web;

namespace Curiosus.Hosting.Web
{
    /// <summary>
    /// Extension methods for <see cref="WebApplicationBuilder"/> to set up the standard Curiosus logging.
    /// </summary>
    public static class CuriosusWebLoggingExtensions
    {
        /// <summary>
        /// Does what <see cref="CuriosusLoggingExtensions.AddCuriosusLogging(Microsoft.Extensions.Hosting.IHostApplicationBuilder, Action{CuriosusLoggingOptions}?)"/>
        /// does and also registers the NLog.Web <c>${aspnet-*}</c> layout renderers, so NLog.config needs no
        /// <c>&lt;add assembly="NLog.Web.AspNetCore"/&gt;</c>.
        /// </summary>
        /// <param name="builder">Web application builder.</param>
        /// <param name="configure">Overrides the options bound from the <c>Log</c> section.</param>
        /// <returns>Options builder of <see cref="CuriosusLoggingOptions"/>.</returns>
        public static OptionsBuilder<CuriosusLoggingOptions> AddCuriosusLogging(
            this WebApplicationBuilder builder,
            Action<CuriosusLoggingOptions>? configure = null) =>
            CuriosusLoggingExtensions.AddCuriosusLogging(builder, configure, extensions => extensions.RegisterNLogWeb());
    }
}
