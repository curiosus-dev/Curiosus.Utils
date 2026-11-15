using System;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Tools;
using Curiosus.Tools.AppInitializer;
using Microsoft.Extensions.Logging;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Initializer of <see cref="FireAndForget"/>.
    /// </summary>
    /// <remarks>
    /// Sets its logger until <c>FireAndForget</c> is replaced (#49).
    /// </remarks>
    internal sealed class FireAndForgetInitializer : IAppInitializer
    {
        private readonly ILoggerFactory _loggerFactory;

        public FireAndForgetInitializer(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc />
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var logger = _loggerFactory.CreateLogger(typeof(FireAndForget));
            FireAndForget.SetLogger(logger);
            
            return Task.CompletedTask;
        }
    }
}