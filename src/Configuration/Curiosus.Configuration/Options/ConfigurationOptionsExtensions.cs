using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Curiosus.Configuration;

/// <summary>
/// Reads options from <see cref="IConfiguration"/> before the host is built.
/// </summary>
public static class ConfigurationOptionsExtensions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to the configuration section and validates it the way
    /// <see cref="OptionsServiceCollectionExtensions.AddCuriosusOptions{TOptions}(Microsoft.Extensions.DependencyInjection.IServiceCollection, string)"/>
    /// does, for code that needs the values while the builder is still being set up (before <c>IOptions</c> exists).
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="configSectionPath">Path of the section, for example <c>"Log"</c>.</param>
    /// <param name="configure">Applied after binding, like <c>OptionsBuilder.Configure</c>.</param>
    /// <exception cref="OptionsValidationException">The options are invalid.</exception>
    public static TOptions GetCuriosusOptions<TOptions>(
        this IConfiguration configuration,
        string configSectionPath,
        Action<TOptions>? configure = null)
        where TOptions : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

        var options = new TOptions();
        configuration.GetSection(configSectionPath).Bind(options);
        configure?.Invoke(options);

        var result = new DataAnnotationValidateOptions<TOptions>(Options.DefaultName).Validate(Options.DefaultName, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(TOptions), result.Failures);
        }

        return options;
    }
}
