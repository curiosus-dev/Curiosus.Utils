using Curiosus.Hosting;
using Curiosus.RequestProcessing.RabbitMQ;
using Curiosus.RequestProcessing.RabbitMQ.Sample.Common;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.RequestProcessing;
using Curiosus.RequestProcessing.Workers;
using Microsoft.Extensions.DependencyInjection;

var builder = CuriosusHost.CreateBuilder(args).AddCuriosusDefaults();
builder.AddCuriosusNLog();

// options are bound from the "RequestProcessor" section and validated when the host starts
builder.Services
    .AddRabbitMQRequestProcessor<
        SampleRequest,
        SampleRequestProcessingWorker,
        WorkerBasicExtraParams,
        SampleRequestProcessingBootstrapper,
        SampleRequestProcessorNodeOptions,
        SampleRequestDispatcher,
        SampleRequestProcessingInfo>()
    .BindConfiguration("RequestProcessor");

builder.Services.AddSingleton<SampleRequestProcessingMetricsCollector>();
builder.Services.AddHostedService(x => x.GetRequiredService<SampleRequestProcessingMetricsCollector>());

return await CuriosusApp.RunAsync(builder.Build());
