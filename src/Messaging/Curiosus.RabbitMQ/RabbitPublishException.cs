using System;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Thrown by <see cref="IRabbitPublisher.PublishAsync{T}"/> when the broker didn't confirm the message.
/// </summary>
public sealed class RabbitPublishException : Exception
{
    /// <summary>
    /// Why the publish failed.
    /// </summary>
    public RabbitPublishFailure Reason { get; }

    /// <summary>
    /// Exchange of the message.
    /// </summary>
    public string Exchange { get; }

    /// <summary>
    /// Routing key of the message.
    /// </summary>
    public string RoutingKey { get; }

    /// <summary>
    /// <c>message-id</c> of the message.
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// AMQP reply code of a <see cref="RabbitPublishFailure.Returned"/> message (312) or of the channel close of
    /// <see cref="RabbitPublishFailure.ChannelClosed"/> (404 for a missing exchange); <see langword="null"/> otherwise.
    /// </summary>
    public ushort? ReplyCode { get; }

    /// <summary>
    /// Reply text from the broker that goes with <see cref="ReplyCode"/>.
    /// </summary>
    public string? ReplyText { get; }

    /// <inheritdoc cref="RabbitPublishException"/>
    public RabbitPublishException(
        RabbitPublishFailure reason,
        string exchange,
        string routingKey,
        string messageId,
        ushort? replyCode = null,
        string? replyText = null,
        Exception? innerException = null)
        : base(CreateMessage(reason, exchange, routingKey, messageId, replyCode, replyText), innerException)
    {
        Reason = reason;
        Exchange = exchange;
        RoutingKey = routingKey;
        MessageId = messageId;
        ReplyCode = replyCode;
        ReplyText = replyText;
    }

    private static string CreateMessage(
        RabbitPublishFailure reason,
        string exchange,
        string routingKey,
        string messageId,
        ushort? replyCode,
        string? replyText)
    {
        var description = reason switch
        {
            RabbitPublishFailure.Nacked => "the broker nacked it",
            RabbitPublishFailure.Returned => "no queue is bound for the mandatory message",
            RabbitPublishFailure.ChannelClosed => "the broker closed the channel, the outcome is unknown",
            RabbitPublishFailure.ConnectionLost => "the connection was lost, the outcome is unknown",
            RabbitPublishFailure.Timeout => "it was not confirmed within the publish timeout",
            _ => reason.ToString()
        };

        var message = $"Failed to publish message \"{messageId}\" to exchange \"{exchange}\" "
                      + $"with routing key \"{routingKey}\": {description}";
        if (replyCode != null) message += $" ({replyCode} {replyText})";

        return message;
    }
}
