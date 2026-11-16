using System;
using System.Threading;
using System.Threading.Tasks;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Publishes messages with publisher confirms on the connection of <see cref="IRabbitConnectionProvider"/>.
/// </summary>
/// <remarks>
/// Safe for concurrent use. Exchanges are not declared: declare them at startup, a publish to a missing exchange fails.
/// </remarks>
public interface IRabbitPublisher
{
    /// <summary>
    /// Serializes the message with <see cref="IRabbitMQMessageSerializer"/>, publishes it and completes when the broker
    /// confirmed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While the connection is down or recovering, the publish waits for it, up to
    /// <see cref="RabbitMQOptions.PublishTimeout"/> for the whole call. A message is sent once: the publisher never
    /// re-sends it. After a <see cref="RabbitPublishException"/> with <see cref="RabbitPublishFailure.ChannelClosed"/>,
    /// <see cref="RabbitPublishFailure.ConnectionLost"/> or <see cref="RabbitPublishFailure.Timeout"/> the message may
    /// have reached the broker: if you publish it again, consumers can get it twice and must be idempotent.
    /// </para>
    /// <para>
    /// A message that no queue is bound for is confirmed and dropped by the broker, unless
    /// <see cref="RabbitPublishProperties.Mandatory"/> is set.
    /// </para>
    /// </remarks>
    /// <param name="exchange">Exchange; <c>""</c> is the default exchange, which routes by queue name.</param>
    /// <param name="routingKey">Routing key.</param>
    /// <param name="message">Message.</param>
    /// <param name="properties">Message properties; <see langword="null"/> for the defaults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="RabbitPublishException">
    /// The broker didn't confirm the message; see <see cref="RabbitPublishException.Reason"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="ObjectDisposedException">The publisher is disposed.</exception>
    Task PublishAsync<T>(
        string exchange,
        string routingKey,
        T message,
        RabbitPublishProperties? properties = null,
        CancellationToken cancellationToken = default);
}
