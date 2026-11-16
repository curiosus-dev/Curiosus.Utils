using System;
using System.Linq;
using Curiosus.RequestProcessing.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Curiosus.RequestProcessing
{
    /// <summary>
    /// Методы расширения по регистрации обработчиков запросов.
    /// </summary>
    public static class RequestProcessorIocExtensions
    {
        /// <summary>
        /// Добавляет основные сервисы для обработки запросов.
        /// </summary>
        /// <remarks>
        /// Registers the worker (transient), the bootstrapper as a hosted service and the options: as
        /// <see cref="IOptions{TOptions}"/>, and as <typeparamref name="TOptions"/> and
        /// <see cref="RequestProcessorNodeOptions"/> (the value of <see cref="IOptions{TOptions}"/>) for workers and
        /// bootstrappers that take them. The options are validated with data annotations when the host starts.
        /// <para>
        /// Only the first call for a <typeparamref name="TOptions"/> binds its section and registers the services and
        /// the validation; a later call adds its <paramref name="configure"/> only (its
        /// <paramref name="configSectionPath"/> is ignored), so errors are reported once and the processor runs once.
        /// </para>
        /// </remarks>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Configuration section the options are bound from; a missing section binds
        /// nothing, so the defaults and <paramref name="configure"/> apply.</param>
        /// <param name="configure">Configures the options in code, after binding.</param>
        /// <returns>Options builder of <typeparamref name="TOptions"/>, validated at start.</returns>
        public static OptionsBuilder<TOptions> AddRequestProcessor<
            TRequest,
            TWorker,
            TWorkerParams,
            TProcessorBootstrapper,
            TOptions,
            TDispatcher,
            TProcessingRequestInfo>(
            this IServiceCollection services,
            string configSectionPath = RequestProcessorNodeOptions.SectionName,
            Action<TOptions>? configure = null)
            where TRequest : IRequest
            where TWorkerParams : class, IWorkerExtraParams
            where TDispatcher : RequestDispatcherBase<TRequest, TWorker, TWorkerParams, TProcessingRequestInfo, TOptions>
            where TWorker : WorkerBase<TRequest, TWorkerParams, TProcessingRequestInfo, TOptions>
            where TOptions : RequestProcessorNodeOptions
            where TProcessorBootstrapper : RequestProcessorBootstrapperBase<TRequest, TWorkerParams, TWorker, TDispatcher, TProcessingRequestInfo, TOptions>
            where TProcessingRequestInfo : class, IProcessingRequestInfo
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

            var isRegistered = services.Any(d => d.ServiceType == typeof(RegistrationMarker<TOptions>));
            var optionsBuilder = services.AddOptions<TOptions>();
            if (isRegistered)
            {
                if (configure != null) optionsBuilder.Configure(configure);
                return optionsBuilder;
            }

            services.AddSingleton<RegistrationMarker<TOptions>>();
            optionsBuilder.BindConfiguration(configSectionPath);
            if (configure != null) optionsBuilder.Configure(configure);
            optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

            services.AddTransient<TWorker>();

            services.AddSingleton<TProcessorBootstrapper>();
            services.AddSingleton<IHostedService>(p => p.GetRequiredService<TProcessorBootstrapper>());

            services.AddSingleton(p => p.GetRequiredService<IOptions<TOptions>>().Value);
            if (typeof(TOptions) != typeof(RequestProcessorNodeOptions))
                services.AddSingleton<RequestProcessorNodeOptions>(p => p.GetRequiredService<IOptions<TOptions>>().Value);

            return optionsBuilder;
        }

        /// <summary>
        /// Marks that the request processor with <typeparamref name="TOptions"/> is registered.
        /// </summary>
        private sealed class RegistrationMarker<TOptions>
        {
        }
    }
}
