using System;
using Curiosus.Tools.TempFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Curiosus.SFTP.SSH.Net
{
    /// <inheritdoc />
    /// <remarks>
    /// Every client takes the current <see cref="SftpClientOptions"/>, so a configuration reload (a rotated password or
    /// host key) applies to the next client.
    /// </remarks>
    public class SftpClientFactory : ISftpClientFactory
    {
        private readonly IOptionsMonitor<SftpClientOptions> _options;
        private readonly ITempFileStreamFactory _tempFileStreamFactory;
        private readonly ILoggerFactory _loggerFactory;

        /// <inheritdoc cref="SftpClientFactory"/>
        public SftpClientFactory(
            IOptionsMonitor<SftpClientOptions> options,
            ITempFileStreamFactory tempFileStreamFactory,
            ILoggerFactory loggerFactory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _tempFileStreamFactory = tempFileStreamFactory ?? throw new ArgumentNullException(nameof(tempFileStreamFactory));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <inheritdoc />
        public ISftpClient GetSftpClient()
        {
            return new SftpClient(_options.CurrentValue, _tempFileStreamFactory, _loggerFactory);
        }
    }
}
