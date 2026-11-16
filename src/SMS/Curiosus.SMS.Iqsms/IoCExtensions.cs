using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.SMS.Iqsms;

/// <summary>
/// Registration of the iqsms.ru sender.
/// </summary>
public static class IoCExtensions
{
    /// <summary>
    /// Adds <see cref="IIqsmsSender"/> (and by default <see cref="ISmsSender"/>), the named <see cref="HttpClient"/>
    /// <see cref="IqsmsSender.HttpClientName"/> and <see cref="IqsmsOptions"/> validated when the host starts.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Configures options in code; bind them from configuration with
    /// <c>.BindConfiguration("Iqsms")</c> on the result.</param>
    /// <param name="useAsDefaultSender">Registers the sender as <see cref="ISmsSender"/> too, unless one is registered.</param>
    /// <returns>Options builder, validated at start.</returns>
    public static OptionsBuilder<IqsmsOptions> AddIqsmsSender(
        this IServiceCollection services,
        Action<IqsmsOptions>? configure = null,
        bool useAsDefaultSender = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<IqsmsOptions>();
        if (configure != null) optionsBuilder.Configure(configure);
        optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

        services.AddHttpClient(IqsmsSender.HttpClientName);
        services.TryAddSingleton<IIqsmsSender, IqsmsSender>();

        if (useAsDefaultSender)
        {
            services.TryAddSingleton<ISmsSender>(s => s.GetRequiredService<IIqsmsSender>());
        }

        return optionsBuilder;
    }
}
