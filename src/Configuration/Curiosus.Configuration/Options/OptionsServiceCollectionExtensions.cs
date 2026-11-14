using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.Configuration;

/// <summary>
/// Registration of options bound from configuration and validated at start.
/// </summary>
public static class OptionsServiceCollectionExtensions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to the configuration section, validates it with data annotations
    /// (nested objects and collections with <c>[ValidateObjectMembers]</c>/<c>[ValidateEnumeratedItems]</c>) and
    /// validates it when the host starts, so invalid configuration stops the app before it serves anything.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configSectionPath">Path of the section, for example <c>"Smtp"</c> or <c>"Processors:Orders"</c>.</param>
    /// <returns>Options builder for more validation (<c>.Validate(...)</c>) or post-configuration.</returns>
    public static OptionsBuilder<TOptions> AddCuriosusOptions<TOptions>(
        this IServiceCollection services,
        string configSectionPath)
        where TOptions : class
        => services.AddCuriosusOptions<TOptions>(Options.DefaultName, configSectionPath);

    /// <summary>
    /// Binds the named <typeparamref name="TOptions"/> instance to the configuration section and validates it at start,
    /// like <see cref="AddCuriosusOptions{TOptions}(IServiceCollection, string)"/>. Read it with
    /// <c>IOptionsMonitor&lt;TOptions&gt;.Get(name)</c> or <c>IOptionsSnapshot&lt;TOptions&gt;.Get(name)</c>.
    /// </summary>
    public static OptionsBuilder<TOptions> AddCuriosusOptions<TOptions>(
        this IServiceCollection services,
        string name,
        string configSectionPath)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

        return services.AddOptions<TOptions>(name)
            .BindConfiguration(configSectionPath)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
