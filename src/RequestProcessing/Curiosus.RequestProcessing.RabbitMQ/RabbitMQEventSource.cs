using System;
using Curiosus.RequestProcessing.RabbitMQ.Options;

namespace Curiosus.RequestProcessing.RabbitMQ;

/// <summary>
/// RabbitMQ event source to listen.
/// </summary>
public class RabbitMQEventSource : IEventSource
{
    /// <summary>
    /// Options to connect to RabbitMQ.
    /// </summary>
    public RabbitMQEventReceiverOptions RabbitMQOptions { get; }

    /// <inheritdoc cref="RabbitMQEventSource"/>
    public RabbitMQEventSource(RabbitMQEventReceiverOptions rabbitMQOptions)
    {
        RabbitMQOptions = rabbitMQOptions ?? throw new ArgumentNullException(nameof(rabbitMQOptions));
    }
}
