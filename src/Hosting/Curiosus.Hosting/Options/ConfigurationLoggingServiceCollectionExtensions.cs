using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Curiosus.Hosting;

/// <summary>
/// Logging of the configuration at startup.
/// </summary>
public static class ConfigurationLoggingServiceCollectionExtensions
{
    /// <summary>
    /// Logs the listed configuration sections at <c>Information</c> before hosted services start: one line per value
    /// with the provider it comes from (file, environment variables, command line), so you can see which source won.
    /// </summary>
    /// <remarks>
    /// Values of keys whose path contains <c>password</c>, <c>pwd</c>, <c>passphrase</c>, <c>secret</c>, <c>token</c>,
    /// <c>credential</c>, <c>apikey</c>, <c>privatekey</c> (not <c>privatekeypath</c>), <c>signature</c>, <c>salt</c> or
    /// a segment ending with <c>key</c> or <c>keys</c>, and values with a URI that has user info
    /// (<c>amqp://user:pass@host</c>), print <c>***</c>; connection strings (paths containing <c>connectionstring</c>) print only well-known
    /// non-secret keys. Sections that aren't listed are not printed at all. Several calls add up.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="sections">Configuration sections to print, for example <c>"Smtp"</c>.</param>
    public static IServiceCollection LogConfigurationOnStart(this IServiceCollection services, params string[] sections)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(sections);
        if (sections.Any(String.IsNullOrWhiteSpace))
            throw new ArgumentException("Section names can't be empty.", nameof(sections));

        var registered = services.FirstOrDefault(d => d.ServiceType == typeof(ConfigurationStartupLoggerSections))
            ?.ImplementationInstance as ConfigurationStartupLoggerSections;
        if (registered == null)
        {
            registered = new ConfigurationStartupLoggerSections();
            services.AddSingleton(registered);
        }

        foreach (var section in sections)
        {
            if (!registered.Sections.Contains(section, StringComparer.OrdinalIgnoreCase)) registered.Sections.Add(section);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ConfigurationStartupLogger>());
        return services;
    }
}
