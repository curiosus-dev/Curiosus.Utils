using Curiosus.Configuration;
using Curiosus.Hosting;
using Curiosus.RabbitMQ;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Configuration;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = CuriosusHost.CreateBuilder(args).AddCuriosusDefaults();
builder.AddCuriosusNLog();

// the options class still uses the old validation model, so it is bound once and registered as is
var configuration = builder.Configuration.Get<SampleProducerAppConfiguration>() ?? new SampleProducerAppConfiguration();
configuration.AssertValid();
builder.Services.AddSingleton(configuration);
builder.Services.AddCuriosusRabbitMQ();
builder.Services.AddHostedService<SampleRequestsProducer>();

return await CuriosusApp.RunAsync(builder.Build());
