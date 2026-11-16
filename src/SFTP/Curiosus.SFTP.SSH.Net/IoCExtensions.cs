using System;
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
        /// Adds SSH.NET SFTP services: <see cref="SftpClientOptions"/> validated on start, <see cref="ISftpClientFactory"/>
        /// and an app initializer that checks the connection when <see cref="SftpClientOptions.CheckOnStart"/> is set.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures options in code; bind them from configuration with
        /// <c>.BindConfiguration("Sftp")</c> on the result.</param>
        /// <returns>Options builder, validated at start.</returns>
        /// <remarks>
        /// <see cref="ISftpClient"/> needs <c>ITempFileStreamFactory</c> of <c>Curiosus.Tools</c>: register it with
        /// <c>AddTempFileServices</c>.
        /// </remarks>
        public static OptionsBuilder<SftpClientOptions> AddSftpServices(
            this IServiceCollection services,
            Action<SftpClientOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var builder = services.AddOptions<SftpClientOptions>();
            if (configure != null) builder.Configure(configure);
            builder.ValidateDataAnnotations().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<SftpClientOptions>, SftpClientOptionsValidator>());

            services.TryAddSingleton<ISftpClientFactory, SftpClientFactory>();
            // not AddAppInitializer: it adds one more initializer on every call, a second AddSftpServices would check twice
            services.TryAddEnumerable(ServiceDescriptor.Transient<IAppInitializer, SftpInitializer>());

            return builder;
        }
    }
}
