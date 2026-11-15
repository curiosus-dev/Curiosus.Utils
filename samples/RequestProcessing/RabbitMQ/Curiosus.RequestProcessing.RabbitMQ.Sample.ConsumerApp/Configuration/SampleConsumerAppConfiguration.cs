using Curiosus.Configuration;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.RequestProcessing;

namespace Curiosus.RequestProcessing.RabbitMQ.Sample.ConsumerApp.Configuration;

/// <summary>
/// Configuration of a sample consumer app.
/// </summary>
public class SampleConsumerAppConfiguration : IValidatableOptions
{
    /// <summary>
    /// Options of request processor.
    /// </summary>
    public SampleRequestProcessorNodeOptions RequestProcessor { get; } = new();

    /// <inheritdoc />
    public IReadOnlyCollection<ConfigurationValidationError> Validate(string? prefix = null)
    {
        var errors = new ConfigurationValidationErrorCollection(prefix);

        errors.AddErrors(RequestProcessor.Validate(nameof(RequestProcessor)));

        return errors;
    }
}
