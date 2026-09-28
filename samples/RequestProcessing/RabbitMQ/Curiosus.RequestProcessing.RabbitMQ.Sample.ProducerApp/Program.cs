// See https://aka.ms/new-console-template for more information

using Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Startup;

var bootstrapper = new SampleProducerAppBootstrapper();
return await bootstrapper.RunAsync(args);
