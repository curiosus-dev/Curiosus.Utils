using Curiosus.Hosting;
using Curiosus.RequestProcessing.RabbitMQ.Sample.Common;
using Curiosus.RequestProcessing.RabbitMQ;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.Configuration;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.RequestProcessing;
using Curiosus.RequestProcessing.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.Startup;

/// <summary>
/// Class for bootstrapping sample consumer app.
/// </summary>
public class SampleConsumerAppBootstrapper : CuriosusServiceAppBootstrapper<CuriosusCLIArguments, SampleConsumerAppConfiguration>
{
    /// <inheritdoc cref="SampleConsumerAppBootstrapper"/>
    public SampleConsumerAppBootstrapper()
    {
        // register services
        ConfigureServices((_, services, appConfiguration) =>
        {
            // register request processing
            services.AddRabbitMQRequestProcessor<
                SampleRequest,
                SampleRequestProcessingWorker,
                WorkerBasicExtraParams,
                SampleRequestProcessingBootstrapper,
                SampleRequestProcessorNodeOptions,
                SampleRequestDispatcher,
                SampleRequestProcessingInfo>(appConfiguration.RequestProcessor);

            // register metrics collector
            services.AddSingleton<SampleRequestProcessingMetricsCollector>();
            services.AddHostedService(x => x.GetRequiredService<SampleRequestProcessingMetricsCollector>());
        });
    }
}
