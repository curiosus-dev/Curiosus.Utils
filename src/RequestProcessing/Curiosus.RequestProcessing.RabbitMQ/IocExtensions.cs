using System;
using Curiosus.RequestProcessing.RabbitMQ.Options;
using Curiosus.RequestProcessing.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.RequestProcessing.RabbitMQ;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register RabbitMQ request processing services.
/// </summary>
public static class IocExtensions
{
    /// <summary>
    /// Adds services for processing requests from RabbitMQ.
    /// </summary>
    /// <remarks>
    /// Registers the same services as
    /// <see cref="RequestProcessorIocExtensions.AddRequestProcessor{TRequest,TWorker,TWorkerParams,TProcessorBootstrapper,TOptions,TDispatcher,TProcessingRequestInfo}"/>.
    /// The options, <see cref="RabbitMQRequestProcessorNodeOptions.RabbitMQEventReceiver"/> included, are validated
    /// with data annotations when the host starts. Only the first call for a <typeparamref name="TOptions"/> binds its
    /// section and registers the services and the validation; a later call adds its <paramref name="configure"/> only.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configSectionPath">Configuration section the options are bound from; a missing section binds
    /// nothing, so the defaults and <paramref name="configure"/> apply.</param>
    /// <param name="configure">Configures the options in code, after binding.</param>
    /// <returns>Options builder of <typeparamref name="TOptions"/>, validated at start.</returns>
    public static OptionsBuilder<TOptions> AddRabbitMQRequestProcessor<
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
        where TDispatcher : RabbitMQRequestDispatcherBase<TRequest, TWorker, TWorkerParams, TProcessingRequestInfo, TOptions>
        where TWorker : WorkerBase<RabbitMQRequestWrapper<TRequest>, TWorkerParams, TProcessingRequestInfo, TOptions>
        where TOptions : RabbitMQRequestProcessorNodeOptions
        where TProcessorBootstrapper : RabbitMQRequestProcessorBootstrapperBase<TRequest, TWorkerParams, TWorker, TDispatcher, TProcessingRequestInfo, TOptions>
        where TProcessingRequestInfo : class, IProcessingRequestInfo
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddRequestProcessor<
            RabbitMQRequestWrapper<TRequest>,
            TWorker,
            TWorkerParams,
            TProcessorBootstrapper,
            TOptions,
            TDispatcher,
            TProcessingRequestInfo>(configSectionPath, configure);
    }
}
