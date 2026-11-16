using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Curiosus.RequestProcessing.RabbitMQ.Options;

/// <summary>
/// Options of a request processor node that receives requests from RabbitMQ.
/// </summary>
/// <remarks>
/// Derive from it to add options of the app; <see cref="RabbitMQEventReceiver"/> is validated with the node options.
/// </remarks>
public class RabbitMQRequestProcessorNodeOptions : RequestProcessorNodeOptions
{
    /// <summary>
    /// Options to configure RabbitMQ events receiver.
    /// </summary>
    [Required]
    [ValidateObjectMembers]
    public RabbitMQEventReceiverOptions RabbitMQEventReceiver { get; set; } = new();
}
