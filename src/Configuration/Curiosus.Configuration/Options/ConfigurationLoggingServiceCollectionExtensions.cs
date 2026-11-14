using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Curiosus.Configuration;

/// <summary>
/// Logging of the configuration at startup.
/// </summary>
public static class ConfigurationLoggingServiceCollectionExtensions
{
    /// <summary>
    /// Logs the configuration at <c>Information</c> before hosted services start: one line per value with the provider
    /// it comes from (file, environment variables, command line), so you can see which source won.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without sections the whole configuration is printed, except environment variables without a section
    /// (<c>PATH</c>, <c>HOSTNAME</c>, ...) that no other provider sets; <c>Smtp__Password</c> is printed as
    /// <c>Smtp:Password</c>. With sections only they are printed. Several calls add up; use
    /// <see cref="ConfigurationStartupLogOptions.ExcludedSections"/> to leave sections out.
    /// </para>
    /// <para>
    /// Values of keys whose path contains <c>password</c>, <c>pwd</c>, <c>passphrase</c>, <c>secret</c>, <c>token</c>,
    /// <c>credential</c>, <c>apikey</c>, <c>privatekey</c> (not <c>privatekeypath</c>), <c>signature</c>, <c>salt</c> or
    /// a segment ending with <c>key</c> or <c>keys</c>, and values that carry a secret themselves (a URI with user info,
    /// a URL with <c>?token=</c>, <c>?sig=</c>, <c>?key=</c>, <c>?code=</c>, a webhook URL), print <c>***</c>; connection strings (paths containing <c>connectionstring</c>)
    /// print only well-known non-secret keys.
    /// </para>
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="sections">Sections to print, for example <c>"Smtp"</c>; none prints the whole configuration.</param>
    public static IServiceCollection LogConfigurationOnStart(this IServiceCollection services, params string[] sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        if (sections.Any(String.IsNullOrWhiteSpace))
            throw new ArgumentException("Section names can't be empty.", nameof(sections));

        return services.LogConfigurationOnStart(options => options.Sections.AddRange(sections));
    }

    /// <summary>
    /// Logs the configuration at <c>Information</c> before hosted services start, configured in code.
    /// </summary>
    /// <inheritdoc cref="LogConfigurationOnStart(IServiceCollection, string[])" path="/remarks"/>
    public static IServiceCollection LogConfigurationOnStart(
        this IServiceCollection services,
        Action<ConfigurationStartupLogOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ConfigurationStartupLogger>());
        return services;
    }
}
