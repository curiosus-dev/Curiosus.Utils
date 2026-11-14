using System;
using System.Threading;

namespace Curiosus.DateTime.DbSync;

/// <summary>
/// Clock consistent with the database clock: the local UTC time shifted by the offset "database − local",
/// which <see cref="DbTimeSyncService"/> measures periodically.
/// </summary>
/// <remarks>
/// <para>
/// Only <see cref="GetUtcNow"/> is shifted. <see cref="GetTimestamp"/>, timers and the local time zone come from the
/// local clock, so intervals and delays stay monotonic.
/// </para>
/// <para>
/// "Now" can jump backwards when a resync lowers the offset: measure intervals with <see cref="GetTimestamp"/> or
/// <see cref="TimeProvider.GetElapsedTime(long)"/>, not by subtracting two <see cref="GetUtcNow"/> values.
/// </para>
/// </remarks>
public sealed class DbSyncTimeProvider : TimeProvider
{
    private long _offsetTicks;
    private int _isSynchronized;

    /// <summary>
    /// Creates a provider over the system clock.
    /// </summary>
    public DbSyncTimeProvider() : this(System)
    {
    }

    /// <summary>
    /// Creates a provider over the specified local clock, for example a <c>FakeTimeProvider</c> in tests.
    /// </summary>
    public DbSyncTimeProvider(TimeProvider localClock)
    {
        LocalClock = localClock ?? throw new ArgumentNullException(nameof(localClock));
    }

    /// <summary>
    /// Local clock the offset is applied to; timers and delays use it.
    /// </summary>
    public TimeProvider LocalClock { get; }

    /// <summary>
    /// Offset "database − local" added to the local time; <see cref="TimeSpan.Zero"/> until the first successful sync.
    /// </summary>
    public TimeSpan Offset => TimeSpan.FromTicks(Volatile.Read(ref _offsetTicks));

    /// <summary>
    /// <c>false</c> until the first successful sync: until then <see cref="GetUtcNow"/> returns the local time.
    /// </summary>
    public bool IsSynchronized => Volatile.Read(ref _isSynchronized) == 1;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => LocalClock.GetUtcNow() + Offset;

    /// <inheritdoc />
    public override long GetTimestamp() => LocalClock.GetTimestamp();

    /// <inheritdoc />
    public override long TimestampFrequency => LocalClock.TimestampFrequency;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => LocalClock.LocalTimeZone;

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => LocalClock.CreateTimer(callback, state, dueTime, period);

    internal void SetOffset(TimeSpan offset)
    {
        Volatile.Write(ref _offsetTicks, offset.Ticks);
        Volatile.Write(ref _isSynchronized, 1);
    }
}
