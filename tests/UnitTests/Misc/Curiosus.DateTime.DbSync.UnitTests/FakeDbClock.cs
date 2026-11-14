using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;

namespace Curiosus.DateTime.DbSync.UnitTests;

/// <summary>
/// Database clock that is <see cref="Offset"/> ahead of the local fake clock and takes <see cref="Rtt"/> to answer,
/// reading its clock in the middle of the round trip.
/// </summary>
internal sealed class FakeDbClock(FakeTimeProvider localClock) : IDbClock
{
    private int _calls;

    public TimeSpan Offset { get; set; }

    public TimeSpan Rtt { get; set; }

    public Exception? Failure { get; set; }

    /// <summary>
    /// When set, a call waits for it, so a test can hold the database "unresponsive".
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    public int Calls => Volatile.Read(ref _calls);

    public async Task<DateTimeOffset> GetUtcNowAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        if (Gate != null) await Gate.Task.WaitAsync(cancellationToken);
        if (Failure != null) throw Failure;

        localClock.Advance(Rtt / 2);
        var dbNow = localClock.GetUtcNow() + Offset;
        localClock.Advance(Rtt / 2);
        return dbNow;
    }
}
