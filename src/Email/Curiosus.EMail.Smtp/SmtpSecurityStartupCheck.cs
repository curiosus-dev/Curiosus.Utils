using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Curiosus.EMail.Smtp
{
    /// <summary>
    /// Fails the host start when <see cref="ISmtpEMailOptions.AllowInvalidServerCertificate"/> is set in <c>Production</c>
    /// and warns about it in other environments.
    /// </summary>
    internal sealed class SmtpSecurityStartupCheck : IHostedService
    {
        private readonly ISmtpEMailOptions _options;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<SmtpSecurityStartupCheck> _logger;

        public SmtpSecurityStartupCheck(
            ISmtpEMailOptions options,
            IHostEnvironment environment,
            ILogger<SmtpSecurityStartupCheck> logger)
        {
            _options = options;
            _environment = environment;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.AllowInvalidServerCertificate) return Task.CompletedTask;

            if (_environment.IsProduction())
                throw new InvalidOperationException(
                    "AllowInvalidServerCertificate of the SMTP options is set in Production: the SMTP credentials and messages " +
                    "would be sent to any server that presents a certificate. Fix the server certificate or trust its CA instead.");

            _logger.LogWarning(
                "SMTP server certificates are not validated (AllowInvalidServerCertificate) for {SmtpServer}: use it only for a development relay",
                _options.SmtpServer);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
