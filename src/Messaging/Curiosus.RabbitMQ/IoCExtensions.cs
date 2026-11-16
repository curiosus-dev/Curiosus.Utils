using System;
using Curiosus.Configuration;
using Curiosus.RabbitMQ.RPC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Registration of the RabbitMQ services.
/// </summary>
public static class IoCExtensions
{
    /// <summary>
    /// Binds <see cref="RabbitMQOptions"/> to the configuration section, validates them when the host starts and
    /// registers <see cref="IRabbitConnectionProvider"/>, <see cref="IRabbitMQMessageSerializer"/>,
    /// <see cref="IRabbitPublisher"/> and <see cref="RabbitMqRpcClientFactory"/> as singletons.
    /// </summary>
    /// <remarks>
    /// Services are added with <c>TryAdd</c>: register your own <see cref="IRabbitMQMessageSerializer"/> before or after
    /// this call to replace the default one. The connection is opened on first use, not when the host starts.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configSectionPath">
    /// Path of the configuration section, <see cref="RabbitMQOptions.SectionName"/> by default. A missing section binds
    /// nothing: the defaults and <paramref name="configure"/> apply.
    /// </param>
    /// <param name="configure">Configures the options in code, after they are bound from the section.</param>
    public static IServiceCollection AddCuriosusRabbitMQ(
        this IServiceCollection services,
        string configSectionPath = RabbitMQOptions.SectionName,
        Action<RabbitMQOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

        var optionsBuilder = services.AddCuriosusOptions<RabbitMQOptions>(configSectionPath);
        if (configure != null) optionsBuilder.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<RabbitMQOptions>, RabbitMQOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRabbitConnectionProvider, RabbitConnectionProvider>();
        services.TryAddSingleton<IRabbitMQMessageSerializer, RabbitMQJsonMessageSerializer>();
        services.TryAddSingleton<IRabbitPublisher, RabbitPublisher>();
        services.TryAddSingleton<RabbitMqRpcClientFactory>();

        return services;
    }
}
