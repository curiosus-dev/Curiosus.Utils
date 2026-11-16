using System;
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
        /// </remarks>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures the options in code; bind them from configuration with
        /// <c>.BindConfiguration("RequestProcessor")</c> on the result.</param>
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

            var optionsBuilder = services.AddOptions<TOptions>();
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
    }
}
