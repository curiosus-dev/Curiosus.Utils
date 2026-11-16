using System;
using Curiosus.RequestProcessing.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.RequestProcessing.Postgres
{
    /// <summary>
    /// Registration of request processing with PostgreSQL events.
    /// </summary>
    public static class IocExtensions
    {
        /// <summary>
        /// Добавляет основные сервисы для обработки запросов.
        /// </summary>
        /// <remarks>
        /// Registers the same services as
        /// <see cref="RequestProcessorIocExtensions.AddRequestProcessor{TRequest,TWorker,TWorkerParams,TProcessorBootstrapper,TOptions,TDispatcher,TProcessingRequestInfo}"/>.
        /// The options, <see cref="PostgresRequestProcessorNodeOptions.PostgresEventReceiver"/> included, are validated
        /// with data annotations when the host starts.
        /// </remarks>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures the options in code; bind them from configuration with
        /// <c>.BindConfiguration("RequestProcessor")</c> on the result.</param>
        /// <returns>Options builder of <typeparamref name="TOptions"/>, validated at start.</returns>
        public static OptionsBuilder<TOptions> AddPostgresRequestProcessor<
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
            where TOptions : PostgresRequestProcessorNodeOptions
            where TProcessorBootstrapper : PostgresRequestProcessorBootstrapperBase<TRequest, TWorkerParams, TWorker, TDispatcher, TProcessingRequestInfo, TOptions>
            where TProcessingRequestInfo : class, IProcessingRequestInfo
        {
            ArgumentNullException.ThrowIfNull(services);

            return services.AddRequestProcessor<TRequest, TWorker, TWorkerParams, TProcessorBootstrapper, TOptions, TDispatcher, TProcessingRequestInfo>(configure);
        }
    }
}
