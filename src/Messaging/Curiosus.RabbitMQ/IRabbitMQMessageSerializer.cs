using System;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Converts messages to and from the body of RabbitMQ messages.
/// </summary>
/// <remarks>
/// The default serializer writes JSON with <see cref="RabbitMQJson.DefaultOptions"/>. To replace it, register your own
/// implementation as a singleton, before or after <see cref="IoCExtensions.AddCuriosusRabbitMQ"/>. Implementations
/// must be safe for concurrent use.
/// </remarks>
public interface IRabbitMQMessageSerializer
{
    /// <summary>
    /// Content type of the bodies <see cref="Serialize{T}"/> writes, set as the <c>content-type</c> of published
    /// messages; <c>application/json</c> for the default serializer.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Serializes the message to a message body.
    /// </summary>
    ReadOnlyMemory<byte> Serialize<T>(T message);

    /// <summary>
    /// Deserializes a message body.
    /// </summary>
    /// <param name="body">Message body.</param>
    /// <param name="contentType">
    /// <c>content-type</c> of the message; <see langword="null"/> when the message has none, read as <see cref="ContentType"/>.
    /// </param>
    /// <exception cref="NotSupportedException">The serializer can't read <paramref name="contentType"/>.</exception>
    T Deserialize<T>(ReadOnlyMemory<byte> body, string? contentType);
}
