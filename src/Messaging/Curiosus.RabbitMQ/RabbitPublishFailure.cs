namespace Curiosus.RabbitMQ;

/// <summary>
/// Why <see cref="IRabbitPublisher.PublishAsync{T}"/> failed.
/// </summary>
public enum RabbitPublishFailure
{
    /// <summary>
    /// The broker nacked the message: it was not enqueued.
    /// </summary>
    Nacked,

    /// <summary>
    /// The message was <see cref="RabbitPublishProperties.Mandatory"/> and no queue is bound for it (reply code 312).
    /// </summary>
    Returned,

    /// <summary>
    /// The broker closed the channel while the publish was in flight, for example because the exchange doesn't exist
    /// (reply code 404). Other publishes in flight on the channel fail the same way, so the outcome of the message is
    /// unknown.
    /// </summary>
    ChannelClosed,

    /// <summary>
    /// The connection closed while the publish was in flight: the outcome of the message is unknown.
    /// </summary>
    ConnectionLost,

    /// <summary>
    /// No connection, no channel or no confirm within <see cref="RabbitMQOptions.PublishTimeout"/>, for example because
    /// the broker is unreachable or blocked the connection. If the message was sent, its outcome is unknown.
    /// </summary>
    Timeout
}
