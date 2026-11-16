using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Curiosus.RabbitMQ;

/// <summary>
/// The one RabbitMQ connection of the app, opened on first use and recovered automatically.
/// </summary>
/// <remarks>
/// Open channels on it for your own work, and close them; don't close or dispose the connection: the provider closes it
/// when the service provider is disposed.
/// </remarks>
public interface IRabbitConnectionProvider
{
    /// <summary>
    /// Returns the connection, opening it on the first call.
    /// </summary>
    /// <remarks>
    /// Concurrent first calls share one attempt to open the connection. A failed attempt is not remembered: the next
    /// call tries again. The connection recovers automatically after a network failure or a broker restart; until then
    /// it is returned closed (<see cref="IConnection.IsOpen"/> is <see langword="false"/>).
    /// </remarks>
    /// <param name="cancellationToken">
    /// Stops waiting for the connection; the attempt to open it goes on for other callers.
    /// </param>
    /// <exception cref="global::RabbitMQ.Client.Exceptions.BrokerUnreachableException">
    /// The connection could not be opened.
    /// </exception>
    /// <exception cref="System.ObjectDisposedException">The provider is disposed.</exception>
    ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}
