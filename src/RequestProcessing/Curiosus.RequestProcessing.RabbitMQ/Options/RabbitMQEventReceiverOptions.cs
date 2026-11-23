using System;
using System.ComponentModel.DataAnnotations;

namespace Curiosus.RequestProcessing.RabbitMQ.Options;

/// <summary>
/// Options for <see cref="RabbitMQEventReceiver"/>.
/// </summary>
public class RabbitMQEventReceiverOptions
{
    /// <summary>
    /// Host where RabbitMQ located.
    /// </summary>
    [Required]
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Rabbit's user name.
    /// </summary>
    [Required]
    public string UserName { get; set; } = null!;

    /// <summary>
    /// Rabbit's password name.
    /// </summary>
    [Required]
    public string Password { get; set; } = null!;

    /// <summary>
    /// Rabbit's port, 1..65535.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Whether to connect over TLS. Default: <c>false</c>.
    /// </summary>
    /// <remarks>
    /// The server certificate must be trusted by the machine and valid for <see cref="HostName"/>: connect by the name
    /// in the certificate, not by an IP address. Set <see cref="Port"/> to the TLS listener of the broker, 5671 by
    /// default; it is not changed automatically.
    /// </remarks>
    public bool UseTls { get; set; }

    /// <summary>
    /// Name of the client, part of the connection name shown in RabbitMQ.
    /// </summary>
    [Required]
    public string ClientName { get; set; } = Environment.MachineName;

    /// <summary>
    /// Name of queue to receive events.
    /// </summary>
    [Required]
    public string QueueName { get; set; } = null!;

    /// <summary>
    /// Multiplier for QoS to increase inner queue size: at least 1 and less than 20.
    /// </summary>
    [Range(typeof(decimal), "1", "20", MaximumIsExclusive = true, ParseLimitsInInvariantCulture = true)]
    public decimal QosMultiplier { get; set; } = 1;
}
