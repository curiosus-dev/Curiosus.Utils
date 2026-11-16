using System;
using Curiosus.Tools;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Generator of unique and readable correlationId.
/// </summary>
public class CorrelationIdGenerator
{
    private readonly string _clientName;

    /// <inheritdoc cref="CorrelationIdGenerator"/>
    public CorrelationIdGenerator(RabbitMQOptions rabbitMqOptions)
    {
        ArgumentNullException.ThrowIfNull(rabbitMqOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(rabbitMqOptions.ClientName, nameof(rabbitMqOptions));

        _clientName = rabbitMqOptions.ClientName;
    }

    /// <summary>
    /// Generates unique correlation id.
    /// </summary>
    public string GenerateCorrelationId()
    {
        return $"{_clientName}_r{UniqueIdGenerator.Generate().ToPublicId()}";
    }

    /// <summary>
    /// Generates unique correlation id using specified existed correlation id as base.
    /// </summary>
    public string GenerateCorrelationId(string baseCorrelationId)
    {
        if (String.IsNullOrWhiteSpace(baseCorrelationId)) throw new ArgumentNullException(nameof(baseCorrelationId));

        return $"{baseCorrelationId}->{_clientName}_r{UniqueIdGenerator.Generate().ToPublicId()}";
    }
}
