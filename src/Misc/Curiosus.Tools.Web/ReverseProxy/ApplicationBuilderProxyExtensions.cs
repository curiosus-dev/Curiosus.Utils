using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.ReverseProxy
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/>, which include support for running the server behind a proxy
    /// </summary>
    public static class ApplicationBuilderProxyExtensions
    {
        /// <summary>
        /// Enables support for the application to work behind the reverse proxy: forwarded headers and
        /// <see cref="ReverseProxyOptions.PathBase"/> of the options registered by
        /// <see cref="IoCExtensions.AddReverseProxy"/>.
        /// </summary>
        /// <remarks>
        /// Must call before enabling authorization and other handlers
        /// </remarks>
        /// <param name="app">Application builder.</param>
        /// <exception cref="InvalidOperationException"><c>AddReverseProxy</c> wasn't called.</exception>
        public static IApplicationBuilder UseReverseProxy(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var registered = app.ApplicationServices
                .GetServices<IValidateOptions<ReverseProxyOptions>>()
                .OfType<ReverseProxyOptionsValidator>()
                .Any();
            if (!registered)
                throw new InvalidOperationException("Call services.AddReverseProxy() before app.UseReverseProxy()");

            var options = app.ApplicationServices.GetRequiredService<IOptions<ReverseProxyOptions>>().Value;

            app.UseForwardedHeaders();

            if (!String.IsNullOrWhiteSpace(options.PathBase))
            {
                var pathBase = new PathString(options.PathBase);
                app.UsePathBase(pathBase);
                app.Use((context, next) =>
                {
                    context.Request.PathBase = pathBase;
                    return next();
                });
            }

            return app;
        }
    }
}
