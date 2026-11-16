using Curiosus.Hosting;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Configuration;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Core;
using Microsoft.Extensions.DependencyInjection;

var builder = CuriosusHost.CreateBuilder(args).AddCuriosusDefaults();
builder.AddCuriosusNLog();

// options are bound from the root of the configuration and validated when the host starts
builder.Services.AddOptions<SampleProducerAppConfiguration>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHostedService<SampleRequestsProducer>();

return await CuriosusApp.RunAsync(builder.Build());
