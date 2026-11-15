using Curiosus.Configuration;
using Curiosus.Hosting;
using Curiosus.RequestProcessing.RabbitMQ;
using Curiosus.RequestProcessing.RabbitMQ.Sample.Common;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.Configuration;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.RequestProcessing;
using Curiosus.RequestProcessing.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = CuriosusHost.CreateBuilder(args).AddCuriosusDefaults();
builder.AddCuriosusNLog();

// the options class still uses the old validation model, so it is bound once and registered as is
var configuration = builder.Configuration.Get<SampleConsumerAppConfiguration>() ?? new SampleConsumerAppConfiguration();
configuration.AssertValid();

builder.Services.AddRabbitMQRequestProcessor<
    SampleRequest,
    SampleRequestProcessingWorker,
    WorkerBasicExtraParams,
    SampleRequestProcessingBootstrapper,
    SampleRequestProcessorNodeOptions,
    SampleRequestDispatcher,
    SampleRequestProcessingInfo>(configuration.RequestProcessor);

builder.Services.AddSingleton<SampleRequestProcessingMetricsCollector>();
builder.Services.AddHostedService(x => x.GetRequiredService<SampleRequestProcessingMetricsCollector>());

return await CuriosusApp.RunAsync(builder.Build());
