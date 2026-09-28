using Curiosus.RequestProcessing.Workers;

namespace Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.RequestProcessing;

public class SampleRequestProcessingInfo : IProcessingRequestInfo
{
    /// <inheritdoc />
    public DateTime ProcessingStarted { get; }

    /// <inheritdoc cref="SampleRequestProcessingInfo"/>
    public SampleRequestProcessingInfo(DateTime processingStarted)
    {
        ProcessingStarted = processingStarted;
    }
}
