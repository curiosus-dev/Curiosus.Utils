using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Curiosus.DateTime.DbSync;

/// <summary>
/// Keeps <see cref="DbSyncTimeProvider.Offset"/> in sync with the database clock.
/// </summary>
/// <remarks>
/// <see cref="StartAsync"/> makes the first attempt, bounded by <see cref="DbSyncTimeOptions.InitialSyncTimeout"/>,
/// and never fails the app start: when the database is unreachable or not migrated yet, the clock stays
/// unsynchronized (offset 0) and the service retries every <see cref="DbSyncTimeOptions.RetryPeriod"/>.
/// After the first success it resyncs every <see cref="DbSyncTimeOptions.SyncPeriod"/>; a failed resync keeps the
/// previous offset.
/// </remarks>
public sealed partial class DbTimeSyncService : BackgroundService
{
    private readonly DbSyncTimeProvider _timeProvider;
    private readonly IDbClock _dbClock;
    private readonly DbSyncTimeOptions _options;
    private readonly ILogger<DbTimeSyncService> _logger;

    /// <inheritdoc cref="DbTimeSyncService"/>
    public DbTimeSyncService(
        DbSyncTimeProvider timeProvider,
        IDbClock dbClock,
        IOptions<DbSyncTimeOptions> options,
        ILogger<DbTimeSyncService> logger)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _dbClock = dbClock ?? throw new ArgumentNullException(nameof(dbClock));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_options.InitialSyncTimeout, _timeProvider.LocalClock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await SyncOnceAsync(linked.Token);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogInitialSyncFailed(ex, _options.RetryPeriod);
        }

        await base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!_timeProvider.IsSynchronized)
        {
            await Task.Delay(_options.RetryPeriod, _timeProvider.LocalClock, stoppingToken);
            await TrySyncAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(_options.SyncPeriod, _timeProvider.LocalClock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TrySyncAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Measures the offset once and applies it, unless the round trip took longer than
    /// <see cref="DbSyncTimeOptions.MaxAcceptedRtt"/>.
    /// </summary>
    /// <returns><c>true</c> when the offset was applied.</returns>
    internal async Task<bool> SyncOnceAsync(CancellationToken cancellationToken)
    {
        var localClock = _timeProvider.LocalClock;
        var startTimestamp = localClock.GetTimestamp();
        var localBefore = localClock.GetUtcNow();
        var dbNow = await _dbClock.GetUtcNowAsync(cancellationToken);
        var rtt = localClock.GetElapsedTime(startTimestamp);

        if (rtt > _options.MaxAcceptedRtt)
        {
            LogSampleRejected(rtt, _options.MaxAcceptedRtt);
            return false;
        }

        // The database read its clock somewhere within the round trip: assume the middle.
        var offset = dbNow - (localBefore + rtt / 2);
        _timeProvider.SetOffset(offset);
        LogSynchronized(offset, rtt);
        return true;
    }

    private async Task TrySyncAsync(CancellationToken stoppingToken)
    {
        try
        {
            await SyncOnceAsync(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            LogSyncFailed(ex, _timeProvider.Offset);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Initial sync with the database clock failed, the app starts unsynchronized and retries every {RetryPeriod}")]
    private partial void LogInitialSyncFailed(Exception exception, TimeSpan retryPeriod);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sync with the database clock failed, keeping offset {Offset}")]
    private partial void LogSyncFailed(Exception exception, TimeSpan offset);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Database clock sample discarded: round trip {Rtt} is longer than {MaxAcceptedRtt}")]
    private partial void LogSampleRejected(TimeSpan rtt, TimeSpan maxAcceptedRtt);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Synchronized with the database clock: offset {Offset}, round trip {Rtt}")]
    private partial void LogSynchronized(TimeSpan offset, TimeSpan rtt);
}
