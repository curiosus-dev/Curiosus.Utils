using System.Collections.Generic;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Properties of a message published by <see cref="IRabbitPublisher"/>.
/// </summary>
public sealed class RabbitPublishProperties
{
    /// <summary>
    /// <c>message-id</c>; a new GUID v7 when not set. Set it to deduplicate a message published again after a failure.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// <c>correlation-id</c>.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Whether the broker writes the message to disk (delivery mode 2), so it survives a broker restart in a durable
    /// queue. Default: <see langword="true"/>.
    /// </summary>
    public bool Persistent { get; init; } = true;

    /// <summary>
    /// Whether the publish fails with <see cref="RabbitPublishFailure.Returned"/> when no queue is bound for the message.
    /// Default: <see langword="false"/>, the broker confirms and drops such a message.
    /// </summary>
    public bool Mandatory { get; init; }

    /// <summary>
    /// Message headers. RabbitMQ.Client adds its <c>x-dotnet-pub-seq-no</c> header to every published message.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Headers { get; init; }
}
