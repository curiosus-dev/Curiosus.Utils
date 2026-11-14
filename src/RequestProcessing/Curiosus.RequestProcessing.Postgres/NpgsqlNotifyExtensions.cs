using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Curiosus.RequestProcessing.Postgres
{
    /// <summary>
    /// Sends PostgreSQL notifications that wake up request processors listening on a channel.
    /// </summary>
    public static class NpgsqlNotifyExtensions
    {
        private const string NotifySql = "SELECT pg_notify(@channel, @payload)";

        /// <summary>
        /// Sends a notification to the channel with <c>pg_notify</c>.
        /// </summary>
        /// <param name="connection">Open connection. When it has an open transaction, the notification is sent on commit
        /// and dropped on rollback: insert the request, notify, commit.</param>
        /// <param name="channel">Channel name, used exactly as written (case-sensitive), as in <c>EventNames</c>.</param>
        /// <param name="payload">Optional payload, shorter than 8000 bytes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentException">The channel name is empty, whitespace, contains NUL or is longer than
        /// 63 bytes in UTF-8.</exception>
        public static async Task NotifyAsync(
            this NpgsqlConnection connection,
            string channel,
            string? payload = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(connection);
            PgChannelName.AssertValid(channel, nameof(channel));

            await using var command = new NpgsqlCommand(NotifySql, connection);
            AddParameters(command, channel, payload);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Sends a notification to the channel with <c>pg_notify</c> on a connection of the data source,
        /// outside any transaction.
        /// </summary>
        /// <param name="dataSource">Data source.</param>
        /// <param name="channel">Channel name, used exactly as written (case-sensitive), as in <c>EventNames</c>.</param>
        /// <param name="payload">Optional payload, shorter than 8000 bytes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentException">The channel name is empty, whitespace, contains NUL or is longer than
        /// 63 bytes in UTF-8.</exception>
        public static async Task NotifyAsync(
            this NpgsqlDataSource dataSource,
            string channel,
            string? payload = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dataSource);
            PgChannelName.AssertValid(channel, nameof(channel));

            await using var command = dataSource.CreateCommand(NotifySql);
            AddParameters(command, channel, payload);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static void AddParameters(NpgsqlCommand command, string channel, string? payload)
        {
            command.Parameters.AddWithValue("channel", channel);
            command.Parameters.AddWithValue("payload", payload ?? String.Empty);
        }
    }
}
