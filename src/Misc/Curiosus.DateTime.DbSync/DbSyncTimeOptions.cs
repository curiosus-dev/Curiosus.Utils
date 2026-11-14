using System;
using Microsoft.Extensions.Options;

namespace Curiosus.DateTime.DbSync;

/// <summary>
/// Options of <see cref="DbTimeSyncService"/>.
/// </summary>
public class DbSyncTimeOptions
{
    /// <summary>
    /// Period of resyncs after the first successful one. Default: 30 minutes.
    /// </summary>
    public TimeSpan SyncPeriod { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Period of attempts until the first successful sync. Default: 10 seconds.
    /// </summary>
    public TimeSpan RetryPeriod { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long the app start waits for the first sync; after it the app starts unsynchronized. Default: 5 seconds.
    /// </summary>
    public TimeSpan InitialSyncTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A sample whose round trip to the database took longer is discarded: its error would be too big.
    /// Default: 1 second.
    /// </summary>
    public TimeSpan MaxAcceptedRtt { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Validates <see cref="DbSyncTimeOptions"/>: periods and the RTT limit are positive, the initial timeout is not negative.
/// </summary>
public class DbSyncTimeOptionsValidator : IValidateOptions<DbSyncTimeOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DbSyncTimeOptions options)
    {
        var builder = new ValidateOptionsResultBuilder();
        if (options.SyncPeriod <= TimeSpan.Zero)
            builder.AddError("must be positive", nameof(options.SyncPeriod));
        if (options.RetryPeriod <= TimeSpan.Zero)
            builder.AddError("must be positive", nameof(options.RetryPeriod));
        if (options.InitialSyncTimeout < TimeSpan.Zero)
            builder.AddError("can't be negative", nameof(options.InitialSyncTimeout));
        if (options.MaxAcceptedRtt <= TimeSpan.Zero)
            builder.AddError("must be positive", nameof(options.MaxAcceptedRtt));

        return builder.Build();
    }
}
