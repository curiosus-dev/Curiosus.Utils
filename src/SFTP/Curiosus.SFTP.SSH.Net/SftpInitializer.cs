using System;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Tools.AppInitializer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Curiosus.SFTP.SSH.Net
{
    /// <summary>
    /// Service for initialization SFTP connections
    /// </summary>
    public class SftpInitializer : IAppInitializer
    {
        private readonly ILogger _logger;
        private readonly ISftpClientFactory _sftpClientFactory;
        private readonly SftpClientOptions _sftpClientOptions;
        
        /// <inheritdoc cref="SftpInitializer"/>
        public SftpInitializer(
            ILogger<SftpInitializer> logger,
            ISftpClientFactory sftpClientFactory,
            IOptions<SftpClientOptions> sftpClientOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sftpClientFactory = sftpClientFactory ?? throw new ArgumentNullException(nameof(sftpClientFactory));
            ArgumentNullException.ThrowIfNull(sftpClientOptions);
            _sftpClientOptions = sftpClientOptions.Value;
        }

        /// <inheritdoc />
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_sftpClientOptions.CheckOnStart)
            {
                _logger.LogInformation("Check connection to ssh storage...");

                using (var sftpClient = _sftpClientFactory.GetSftpClient())
                {
                    sftpClient.CheckConnection();
                }

                _logger.LogInformation("Check connection to ssh storage completed");
            }
            else
            {
                _logger.LogInformation("Check connection to ssh storage disabled");
            }
            return Task.CompletedTask;
        }
    }
}
