using System;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.DateTime.DbSync;

/// <summary>
/// Registration of the database-synchronized clock.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DbSyncTimeProvider"/>, <see cref="DbTimeSyncService"/> that keeps it in sync and the
    /// PostgreSQL <see cref="IDbClock"/> over the <see cref="DbDataSource"/> registered in DI
    /// (for example by <c>AddNpgsqlDataSource</c>), unless another <see cref="IDbClock"/> is registered before.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Configures options in code; bind them from configuration with
    /// <c>.BindConfiguration("DbTimeSync")</c> on the result.</param>
    /// <param name="registerAsTimeProvider">
    /// <c>true</c> replaces <see cref="TimeProvider"/> app-wide with <see cref="DbSyncTimeProvider"/>, whatever was
    /// registered before or is registered later with <c>TryAdd</c>. It also shifts time checks of ASP.NET Core and
    /// other libraries (cookie and token expiry), so by default only <see cref="DbSyncTimeProvider"/> is registered and
    /// injected explicitly.
    /// </param>
    /// <param name="dataSourceKey">Key of a keyed <see cref="DbDataSource"/> registration; <c>null</c> for the
    /// default one. It must point to the primary: a replica has its own clock.</param>
    /// <returns>Options builder, validated at start.</returns>
    public static OptionsBuilder<DbSyncTimeOptions> AddCuriosusDbSyncTimeProvider(
        this IServiceCollection services,
        Action<DbSyncTimeOptions>? configure = null,
        bool registerAsTimeProvider = false,
        object? dataSourceKey = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<DbSyncTimeOptions>();
        if (configure != null) optionsBuilder.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DbSyncTimeOptions>, DbSyncTimeOptionsValidator>());
        optionsBuilder.ValidateOnStart();

        services.TryAddSingleton<IDbClock>(sp => new PostgresDbClock(dataSourceKey == null
            ? sp.GetRequiredService<DbDataSource>()
            : sp.GetRequiredKeyedService<DbDataSource>(dataSourceKey)));
        services.TryAddSingleton(_ => new DbSyncTimeProvider());
        services.AddHostedService<DbTimeSyncService>();

        if (registerAsTimeProvider)
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(sp => sp.GetRequiredService<DbSyncTimeProvider>()));
        }

        return optionsBuilder;
    }
}
