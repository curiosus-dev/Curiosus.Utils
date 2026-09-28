using Curiosus.Hosting;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Configuration;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Startup;

/// <summary>
/// Class for bootstrapping sample producer app.
/// </summary>
public class SampleProducerAppBootstrapper : CuriosusServiceAppBootstrapper<CuriosusCLIArguments, SampleProducerAppConfiguration>
{
    /// <inheritdoc cref="SampleRequestsProducer"/>
    public SampleProducerAppBootstrapper()
    {
        ConfigureServices((_, services, _) =>
        {
            services.AddHostedService<SampleRequestsProducer>();
        });
    } 
}
