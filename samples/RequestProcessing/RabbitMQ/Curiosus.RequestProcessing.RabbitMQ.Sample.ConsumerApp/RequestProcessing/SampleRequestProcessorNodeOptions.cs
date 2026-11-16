using Curiosus.RequestProcessing.RabbitMQ.Options;

namespace Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.RequestProcessing;

/// <summary>
/// Options for request processor.
/// </summary>
/// <remarks>
/// Derives from <see cref="RabbitMQRequestProcessorNodeOptions"/>: the place for options of the app, validated with
/// data annotations together with the inherited ones.
/// </remarks>
public class SampleRequestProcessorNodeOptions : RabbitMQRequestProcessorNodeOptions
{
}
