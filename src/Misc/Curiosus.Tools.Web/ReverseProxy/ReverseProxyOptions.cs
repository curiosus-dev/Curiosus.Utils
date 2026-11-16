using System;
using System.Net;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.ReverseProxy
{
    /// <summary>
    /// Reverse proxy options that may affect the operation of our server
    /// </summary>
    /// <remarks>
    /// Details about working behind a proxy: https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer
    /// </remarks>
    public class ReverseProxyOptions
    {
        /// <summary>Default configuration section of the options.</summary>
        public const string SectionName = "ReverseProxy";

        /// <summary>
        /// Path base under which the proxy publishes the app, starting with <c>/</c>, for example <c>/api</c>;
        /// not set when the app is published at the root.
        /// </summary>
        public string? PathBase { get; set; }

        /// <summary>
        /// IP addresses of the reverse proxies.
        /// </summary>
        /// <remarks>
        /// Forwarded headers are accepted only from known proxies; by default only the loopback address is known.
        /// </remarks>
        public string[]? ProxyIps { get; set; }
    }

    /// <summary>
    /// Validates <see cref="ReverseProxyOptions"/>: <see cref="ReverseProxyOptions.PathBase"/> starts with <c>/</c>,
    /// every <see cref="ReverseProxyOptions.ProxyIps"/> entry is an IP address.
    /// </summary>
    public class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var builder = new ValidateOptionsResultBuilder();
            if (!String.IsNullOrWhiteSpace(options.PathBase) && !options.PathBase.StartsWith('/'))
                builder.AddError("must start with '/'", nameof(options.PathBase));

            if (options.ProxyIps != null)
            {
                for (var i = 0; i < options.ProxyIps.Length; i++)
                {
                    var proxyIp = options.ProxyIps[i];
                    if (!String.IsNullOrWhiteSpace(proxyIp) && !IPAddress.TryParse(proxyIp, out _))
                        builder.AddError($"\"{proxyIp}\" is not an IP address", $"{nameof(options.ProxyIps)}[{i}]");
                }
            }

            return builder.Build();
        }
    }
}
