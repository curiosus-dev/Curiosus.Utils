using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.ReverseProxy
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> that include support for the server running behind a proxy
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Registers <see cref="ReverseProxyOptions"/>, bound from the configuration section and validated when the host
        /// starts, and configures <see cref="ForwardedHeadersOptions"/> from them: <c>X-Forwarded-For</c> and
        /// <c>X-Forwarded-Proto</c> from the loopback address and <see cref="ReverseProxyOptions.ProxyIps"/>. Add the
        /// middleware with <c>app.UseReverseProxy()</c>.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Path of the configuration section, <see cref="ReverseProxyOptions.SectionName"/>
        /// by default. A missing section binds nothing.</param>
        /// <param name="configure">Configures options in code, after the section is bound. Only the first call binds its
        /// section and registers validation; later calls add their <paramref name="configure"/> only.</param>
        /// <returns>Options builder, validated at start.</returns>
        public static OptionsBuilder<ReverseProxyOptions> AddReverseProxy(
            this IServiceCollection services,
            string configSectionPath = ReverseProxyOptions.SectionName,
            Action<ReverseProxyOptions>? configure = null)
        {
            var builder = services.AddSectionOptions(configSectionPath, configure);

            // guarded by the validator registration, so that a second call doesn't add the proxies twice
            if (!IsRegistered(services))
            {
                services.AddSingleton<IValidateOptions<ReverseProxyOptions>, ReverseProxyOptionsValidator>();
                services.AddOptions<ForwardedHeadersOptions>()
                    .Configure<IOptions<ReverseProxyOptions>>((target, source) =>
                    {
                        if (source.Value.ProxyIps != null)
                        {
                            foreach (var proxyIp in source.Value.ProxyIps)
                            {
                                if (String.IsNullOrWhiteSpace(proxyIp)) continue;

                                target.KnownProxies.Add(IPAddress.Parse(proxyIp));
                            }
                        }

                        target.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    });
            }

            return builder;
        }

        private static bool IsRegistered(IServiceCollection services)
        {
            foreach (var descriptor in services)
            {
                if (descriptor.ServiceType == typeof(IValidateOptions<ReverseProxyOptions>)
                    && descriptor.ImplementationType == typeof(ReverseProxyOptionsValidator))
                    return true;
            }

            return false;
        }
    }
}
