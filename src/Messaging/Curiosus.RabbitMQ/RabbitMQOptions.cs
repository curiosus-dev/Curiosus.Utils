using System;
using System.ComponentModel.DataAnnotations;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Options of the RabbitMQ connection, the publisher and the RPC client, bound from configuration by
/// <see cref="IoCExtensions.AddCuriosusRabbitMQ"/>.
/// </summary>
/// <remarks>
/// <see cref="RabbitMQOptionsValidator"/> checks the URI and the timeout when the host starts.
/// </remarks>
public class RabbitMQOptions
{
    /// <summary>
    /// Default configuration section of the options.
    /// </summary>
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// URI of the broker: <c>amqp://user:password@host:5672/vhost</c>, or <c>amqps://</c> for TLS (port 5671 by
    /// default). Escape reserved characters of the user name, the password and the virtual host (<c>%2F</c> for the
    /// default <c>/</c> virtual host is optional: an empty path selects it).
    /// </summary>
    /// <remarks>
    /// The URI carries the password: keep it in secrets, not in a committed configuration file.
    /// For <c>amqps</c> the server certificate must be valid for the host of the URI.
    /// </remarks>
    [Required]
    public Uri ConnectionUri { get; set; } = null!;

    /// <summary>
    /// Name of the client: the connection is named after it in the management UI, and the RPC client uses it in its
    /// connection name (<c>{ClientName}_rpc_client</c>), response queue names and correlation ids. Letters, digits,
    /// <c>.</c>, <c>_</c> and <c>-</c>, up to 64 characters. Default: the machine name.
    /// </summary>
    [Required]
    [RegularExpression("^[A-Za-z0-9._-]{1,64}$")]
    public string ClientName { get; set; } = Environment.MachineName;

    /// <summary>
    /// How long <see cref="IRabbitPublisher.PublishAsync{T}"/> waits for the connection, a channel and the broker's
    /// confirm together; then it fails with <see cref="RabbitPublishFailure.Timeout"/>. Default: 30 seconds.
    /// </summary>
    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of channels the publisher spreads concurrent publishes over, from 1 to 64. Default: 4.
    /// </summary>
    [Range(1, 64)]
    public int PublisherChannelCount { get; set; } = 4;
}
