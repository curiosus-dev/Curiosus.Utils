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
