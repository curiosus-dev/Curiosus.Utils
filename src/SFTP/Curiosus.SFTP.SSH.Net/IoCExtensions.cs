using System;
using System.Linq;
using Curiosus.Tools.AppInitializer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.SFTP.SSH.Net
{
    /// <summary>
    /// Extensions methods for <see cref="IServiceCollection"/> for SSH.NET SFTP services registration
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Adds SSH.NET SFTP services: <see cref="SftpClientOptions"/> bound from the configuration section and validated
        /// on start, <see cref="ISftpClientFactory"/> and an app initializer that checks the connection when
        /// <see cref="SftpClientOptions.CheckOnStart"/> is set.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Configuration section the options are bound from; a missing section binds
        /// nothing, so the defaults and <paramref name="configure"/> apply.</param>
        /// <param name="configure">Configures options in code, after the binding.</param>
        /// <returns>Options builder, validated at start.</returns>
        /// <remarks>
        /// <para>
        /// <see cref="ISftpClient"/> needs <c>ITempFileStreamFactory</c> of <c>Curiosus.Tools</c>: register it with
        /// <c>AddTempFileServices</c>.
        /// </para>
        /// <para>
        /// The section is bound and validation is registered by the first call only; a later call adds its
        /// <paramref name="configure"/> and ignores <paramref name="configSectionPath"/>.
        /// </para>
        /// </remarks>
        public static OptionsBuilder<SftpClientOptions> AddSftpServices(
            this IServiceCollection services,
            string configSectionPath = SftpClientOptions.SectionName,
            Action<SftpClientOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

            var builder = services.AddOptions<SftpClientOptions>();
            // a second binding and a second data annotations validator would bind twice and duplicate every error
            var registered = services.Any(d => d.ServiceType == typeof(IValidateOptions<SftpClientOptions>)
                                               && d.ImplementationType == typeof(SftpClientOptionsValidator));
            if (!registered) builder.BindConfiguration(configSectionPath);
            if (configure != null) builder.Configure(configure);
            if (!registered)
            {
                builder.ValidateDataAnnotations().ValidateOnStart();
                services.AddSingleton<IValidateOptions<SftpClientOptions>, SftpClientOptionsValidator>();
            }

            services.TryAddSingleton<ISftpClientFactory, SftpClientFactory>();
            // not AddAppInitializer: it adds one more initializer on every call, a second AddSftpServices would check twice
            services.TryAddEnumerable(ServiceDescriptor.Transient<IAppInitializer, SftpInitializer>());

            return builder;
        }
    }
}
