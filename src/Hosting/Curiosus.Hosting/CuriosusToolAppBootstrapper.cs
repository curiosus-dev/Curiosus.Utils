using System;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Configuration;
using Curiosus.Hosting.AppInitializer;
using Curiosus.Hosting.Performance;
using Curiosus.Tools.AppInitializer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Bootstrapper for simple apps that requires DI.
    /// </summary>
    public abstract class CuriosusToolAppBootstrapper : CuriosusToolAppBootstrapper<CuriosusCLIArguments, CuriosusAppConfiguration>
    {
    }
    
    /// <summary>
    /// Bootstrapper for simple apps that requires DI.
    /// </summary>
    public abstract class CuriosusToolAppBootstrapper<TArgs> : CuriosusToolAppBootstrapper<TArgs, CuriosusAppConfiguration>
        where TArgs : CuriosusCLIArguments, new()
    {
    }

    /// <summary>
    /// Bootstrapper for simple apps that requires DI.
    /// </summary>
    public abstract class CuriosusToolAppBootstrapper<TArgs, TConfiguration>: CuriosusAppBootstrapper<TArgs, TConfiguration> 
        where TArgs : CuriosusCLIArguments, new() 
        where TConfiguration : class, ICuriosusAppConfiguration, new()
    {
        private Action<IServiceCollection>? _configureServiceAction;
        private Action<IServiceCollection, TConfiguration>? _configureServiceActionWithConfig;
        private Action<IServiceCollection, TConfiguration, TArgs>? _configureServiceActionWithConfigAndArgs;
        
        public CuriosusToolAppBootstrapper<TArgs, TConfiguration> ConfigureServices(Action<IServiceCollection> configureServiceAction)
        {
            _configureServiceAction = configureServiceAction;
            return this;
        }

        public CuriosusToolAppBootstrapper<TArgs, TConfiguration> ConfigureServices(Action<IServiceCollection, TConfiguration> configureServiceAction)
        {
            _configureServiceActionWithConfig = configureServiceAction;
            return this;
        }
        
        public CuriosusToolAppBootstrapper<TArgs, TConfiguration> ConfigureServices(Action<IServiceCollection, TConfiguration, TArgs> configureServiceAction)
        {
            _configureServiceActionWithConfigAndArgs = configureServiceAction;
            return this;
        }

        /// <inheritdoc />
        protected override async Task<int> RunInternalAsync(
            string[] rawArguments,
            TArgs arguments,
            TConfiguration configuration,
            IConfigurationProvider<TConfiguration> configurationProvider,
            string customContentRootDirectory,
            CancellationToken cancellationToken = default)
        {
            // configure service
            var services = new ServiceCollection();
            services.AddSingleton<ILogger>(c =>
            {
                var loggerFactory = c.GetRequiredService<ILoggerFactory>();
                return loggerFactory.CreateLogger("main");
            });
            services.AddLocalization(opt =>
            {
                opt.ResourcesPath = "Resources";
            });
            _configureServiceAction?.Invoke(services);
            _configureServiceActionWithConfig?.Invoke(services, configuration);
            _configureServiceActionWithConfigAndArgs?.Invoke(services, configuration, arguments);
            services.AddAppInitialization();
                
            services.TryAddSingleton(configuration);
            services.TryAddSingleton(arguments);
            services.TryAddSingleton(configurationProvider);
            services.AddPerformanceMeasures();

            services.AddAppInitializer<FireAndForgetInitializer>();
            services.AddLogging(opt =>
            {
                opt.ClearProviders();
                opt.SetMinimumLevel(LogLevel.Trace);
                opt.AddNLog();
            });

            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                await scope.InitAsync(cancellationToken);

                return await ExecuteAsync(sp, rawArguments, arguments, configuration, cancellationToken);
            }
        }

        /// <summary>
        /// Executes specified tool's actions.
        /// </summary>
        protected abstract Task<int> ExecuteAsync(
            IServiceProvider serviceProvider, 
            string[] rawArguments,
            TArgs arguments,
            TConfiguration configuration,
            CancellationToken cancellationToken = default);
    }
}
