using System;
using System.Net.Security;
using RabbitMQ.Client;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Creates RabbitMQ.Client connection factories from <see cref="RabbitMQOptions"/>.
/// </summary>
internal static class RabbitConnectionFactoryBuilder
{
    public static bool IsSupportedScheme(Uri uri)
        => String.Equals(uri.Scheme, "amqp", StringComparison.OrdinalIgnoreCase)
           || String.Equals(uri.Scheme, "amqps", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a factory of connections with automatic and topology recovery.
    /// </summary>
    /// <param name="connectionUri">URI of the broker, validated by <see cref="RabbitMQOptionsValidator"/>.</param>
    /// <param name="clientProvidedName">Name of the connection in the management UI.</param>
    public static ConnectionFactory Create(Uri connectionUri, string clientProvidedName)
    {
        ArgumentNullException.ThrowIfNull(connectionUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientProvidedName);

        var factory = new ConnectionFactory
        {
            Uri = connectionUri,
            ClientProvidedName = clientProvidedName,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        // RabbitMQ.Client accepts a certificate issued for another host when it parses an amqps URI
        if (factory.Ssl.Enabled)
        {
            factory.Ssl.AcceptablePolicyErrors = SslPolicyErrors.None;
        }

        return factory;
    }
}
