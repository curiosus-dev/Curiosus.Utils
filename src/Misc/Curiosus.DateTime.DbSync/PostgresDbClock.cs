using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Curiosus.DateTime.DbSync;

/// <summary>
/// <see cref="IDbClock"/> for PostgreSQL: reads <c>clock_timestamp()</c> as microseconds since the Unix epoch.
/// </summary>
/// <remarks>
/// The query returns a number, not a timestamp, so the result doesn't depend on the session time zone or
/// <c>Npgsql.EnableLegacyTimestampBehavior</c>. <c>clock_timestamp()</c> is the time of the call, unlike <c>now()</c>,
/// which is the start of the transaction.
/// </remarks>
public sealed class PostgresDbClock : IDbClock
{
    private const string Sql = "select (extract(epoch from clock_timestamp()) * 1000000)::bigint";

    private readonly DbDataSource _dataSource;

    /// <param name="dataSource">Data source of the primary database.</param>
    public PostgresDbClock(DbDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset> GetUtcNowAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(Sql);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        var microseconds = Convert.ToInt64(result);

        return DateTimeOffset.UnixEpoch.AddTicks(microseconds * TimeSpan.TicksPerMicrosecond);
    }
}
