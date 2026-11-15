using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Tools;
using Curiosus.Tools.AppInitializer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Prepares the process before hosted services start: applies the <c>Culture</c> section, initializes
    /// <see cref="UniqueIdGenerator"/> and runs every <see cref="IAppInitializer"/> in registration order.
    /// </summary>
    internal sealed class AppInitializerRunner : IHostedLifecycleService
    {
        public const string CultureKey = "Culture:DefaultCulture";
        public const string UniqueIdGeneratorIdKey = "UniqueId:GeneratorId";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AppInitializerRunner> _logger;

        public AppInitializerRunner(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<AppInitializerRunner> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartingAsync(CancellationToken cancellationToken)
        {
            ApplyCulture();
            InitializeUniqueIdGenerator();

            await using var scope = _scopeFactory.CreateAsyncScope();
            foreach (var initializer in scope.ServiceProvider.GetServices<IAppInitializer>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Running initializer {InitializerType}", initializer.GetType());
                await initializer.InitializeAsync(cancellationToken);
            }
        }

        private void ApplyCulture()
        {
            var cultureName = _configuration[CultureKey];
            if (String.IsNullOrWhiteSpace(cultureName)) return;

            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        private void InitializeUniqueIdGenerator()
        {
            var generatorId = _configuration.GetValue<int?>(UniqueIdGeneratorIdKey);
            if (generatorId.HasValue)
            {
                UniqueIdGenerator.Initialize(generatorId.Value);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
