using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Curiosus.Hosting
{
    internal static class CuriosusAppName
    {
        public const string ConfigurationKey = "AppName";

        /// <summary>
        /// The <c>AppName</c> configuration key, or <see cref="IHostEnvironment.ApplicationName"/> when it is not set.
        /// </summary>
        public static string Get(IConfiguration configuration, IHostEnvironment environment) =>
            configuration[ConfigurationKey] is { Length: > 0 } name ? name : environment.ApplicationName;
    }
}
