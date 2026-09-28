using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Curiosus.DAL.Dapper
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to register Curiosus dapper
    /// </summary>
    public static class DapperServiceCollectionExtensions
    {
        /// <summary>
        /// Adds <see cref="DapperSqlExecutor"/> to services as implementation of <see cref="ISqlExecutor"/>
        /// </summary>
        public static IServiceCollection AddCuriosusDapper(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<ISqlExecutor, DapperSqlExecutor>();
            
            return services;
        }
    }
}