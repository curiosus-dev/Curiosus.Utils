using System;
using System.Threading;
using System.Threading.Tasks;

namespace Curiosus.DateTime.DbSync;

/// <summary>
/// Reads the current time of the database.
/// </summary>
/// <remarks>
/// Query the primary: a replica has its own clock.
/// </remarks>
public interface IDbClock
{
    /// <summary>
    /// Returns the current UTC time of the database.
    /// </summary>
    Task<DateTimeOffset> GetUtcNowAsync(CancellationToken cancellationToken);
}
