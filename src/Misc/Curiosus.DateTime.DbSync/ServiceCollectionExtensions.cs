using System;
using System.Data.Common;
using System.Linq;
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
    /// Registers <see cref="DbSyncTimeOptions"/> bound from the configuration section and validated on start,
    /// <see cref="DbSyncTimeProvider"/> as the app-wide <see cref="TimeProvider"/>,
    /// <see cref="DbTimeSyncService"/> that keeps it in sync and the PostgreSQL <see cref="IDbClock"/> over the
    /// <see cref="DbDataSource"/> registered in DI (for example by <c>AddNpgsqlDataSource</c>), unless another
    /// <see cref="IDbClock"/> is registered before.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="TimeProvider"/> is replaced whatever was registered before, and a later <c>TryAdd</c> (for example
    /// the default registration of the host) doesn't override it: every consumer of <see cref="TimeProvider"/>,
    /// ASP.NET Core and other libraries included, follows the database clock. <see cref="DbSyncTimeProvider"/> itself
    /// is registered too, for <see cref="DbSyncTimeProvider.IsSynchronized"/> and <see cref="DbSyncTimeProvider.Offset"/>.
    /// </para>
    /// <para>
    /// The section is bound and validation is registered by the first call only; a later call adds its
    /// <paramref name="configure"/> and ignores <paramref name="configSectionPath"/>.
    /// </para>
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configSectionPath">Configuration section the options are bound from; a missing section binds
    /// nothing, so the defaults and <paramref name="configure"/> apply.</param>
    /// <param name="configure">Configures options in code, after the binding.</param>
    /// <param name="dataSourceKey">Key of a keyed <see cref="DbDataSource"/> registration; <c>null</c> for the
    /// default one. It must point to the primary: a replica has its own clock.</param>
    /// <returns>Options builder, validated at start.</returns>
    public static OptionsBuilder<DbSyncTimeOptions> AddCuriosusDbSyncTimeProvider(
        this IServiceCollection services,
        string configSectionPath = DbSyncTimeOptions.SectionName,
        Action<DbSyncTimeOptions>? configure = null,
        object? dataSourceKey = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

        var optionsBuilder = services.AddOptions<DbSyncTimeOptions>();
        // a second binding would apply the section again over the configure delegates of the earlier calls
        var registered = services.Any(d => d.ServiceType == typeof(IValidateOptions<DbSyncTimeOptions>)
                                           && d.ImplementationType == typeof(DbSyncTimeOptionsValidator));
        if (!registered) optionsBuilder.BindConfiguration(configSectionPath);
        if (configure != null) optionsBuilder.Configure(configure);
        if (!registered)
        {
            optionsBuilder.ValidateOnStart();
            services.AddSingleton<IValidateOptions<DbSyncTimeOptions>, DbSyncTimeOptionsValidator>();
        }

        services.TryAddSingleton<IDbClock>(sp => new PostgresDbClock(dataSourceKey == null
            ? sp.GetRequiredService<DbDataSource>()
            : sp.GetRequiredKeyedService<DbDataSource>(dataSourceKey)));
        services.TryAddSingleton(_ => new DbSyncTimeProvider());
        services.AddHostedService<DbTimeSyncService>();

        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(sp => sp.GetRequiredService<DbSyncTimeProvider>()));

        return optionsBuilder;
    }
}
