using System.ComponentModel.DataAnnotations;
using Curiosus.RabbitMQ;
using Microsoft.Extensions.Options;

namespace Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Configuration;

/// <summary>
/// Options for sample producer app.
/// </summary>
public class SampleProducerAppConfiguration
{
    /// <summary>
    /// RabbitMQ options.
    /// </summary>
    [Required]
    [ValidateObjectMembers]
    public RabbitMQOptions RabbitMQ { get; set; } = new();

    /// <summary>
    /// Name of RabbitMQ queue to send requests.
    /// </summary>
    [Required]
    public string QueueName { get; set; } = null!;

    /// <summary>
    /// Delay between sending requests, ms.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DelayBetweenSendMs { get; set; } = 10;
}
