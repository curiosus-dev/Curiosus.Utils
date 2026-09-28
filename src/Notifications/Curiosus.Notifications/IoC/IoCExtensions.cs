using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Curiosus.Notifications
{
    /// <summary>
    /// Extension methods for IoC for notification classes.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add to IoC service for sending and processing notifications.
        /// </summary>
        public static IServiceCollection AddCuriosusNotificator(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<INotificator, Notificator>();

            return services;
        }

        /// <summary>
        /// Adds specified notification channel.
        /// </summary>
        /// <remarks>
        /// Channel is added as hosted service and notification channel.
        /// </remarks>
        public static IServiceCollection AddCuriosusNotificationChannel<T>(this IServiceCollection services) where T: class, INotificationChannel, IHostedService
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddCuriosusNotificator();
            
            services.TryAddSingleton<T>();
            services.AddSingleton<INotificationChannel>(c => c.GetRequiredService<T>());
            services.AddSingleton<IHostedService>(c => c.GetRequiredService<T>());

            return services;
        }

        /// <summary>
        /// Adds specified notification channel.
        /// </summary>
        /// <remarks>
        /// Channel is added as hosted service and notification channel.
        /// </remarks>
        public static IServiceCollection AddCuriosusNotificationChannel<TType, TImplementation>(this IServiceCollection services) 
            where TType: class, INotificationChannel
            where TImplementation: class, TType, INotificationChannel, IHostedService
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddCuriosusNotificator();
            
            services.TryAddSingleton<TImplementation>();
            services.AddSingleton<TType>(c => c.GetRequiredService<TImplementation>());
            services.AddSingleton<INotificationChannel>(c => c.GetRequiredService<TImplementation>());
            services.AddSingleton<IHostedService>(c => c.GetRequiredService<TImplementation>());

            return services;
        }

        /// <summary>
        /// Adds specified notification builder to IoC.
        /// </summary>
        public static IServiceCollection AddCuriosusNotificationBuilder<T>(this IServiceCollection services) where T : class, INotificationBuilder
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddSingleton<INotificationBuilder, T>();

            return services;
        }
    }
}
