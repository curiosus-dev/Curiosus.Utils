using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Configuration;
using Curiosus.Tools;
using Microsoft.Extensions.Logging;

namespace Curiosus.EMail.Mailgun
{
    /// <inheritdoc />
    public class MailgunEmailSender : IMailgunEmailSender
    {
        /// <summary>
        /// Name of the <see cref="HttpClient"/> requested from <see cref="IHttpClientFactory"/>.
        /// </summary>
        public const string HttpClientName = "Curiosus.EMail.Mailgun";

        private readonly ILogger<MailgunEmailSender> _logger;
        private readonly MailgunEmailOptions _mailgunEmailOptions;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Creates a sender that gets <see cref="HttpClient"/> named <see cref="HttpClientName"/> from the factory.
        /// </summary>
        public MailgunEmailSender(
            ILogger<MailgunEmailSender> logger,
            MailgunEmailOptions mailgunEmailOptions,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            _mailgunEmailOptions = mailgunEmailOptions ?? throw new ArgumentNullException(nameof(mailgunEmailOptions));
            _mailgunEmailOptions.AssertValid();
        }

        private class MailGunResponse
        {
            [JsonPropertyName("message")]
            public string Message { get; set; } = null!;

            [JsonPropertyName("id")]
            public string Id { get; set; } = null!;
        }

        /// <inheritdoc />
        public Task<Response> SendAsync(string toAddress, string subject, string body, bool isBodyHtml = false, CancellationToken cancellationToken = default)
        {
            EmailGuard.AssertToAddress(toAddress);
            EmailGuard.AssertSubject(subject);
            EmailGuard.AssertBody(body);

            return SendAsync(
                toAddress,
                subject,
                body,
                isBodyHtml,
                _mailgunEmailOptions.MailgunUser,
                _mailgunEmailOptions.MailgunApiKey,
                _mailgunEmailOptions.MailgunDomain,
                _mailgunEmailOptions.EmailFrom,
                _mailgunEmailOptions.MailgunRegion,
                _mailgunEmailOptions.ReplyTo,
                cancellationToken);
        }

        private async Task<Response> SendAsync(
            string toAddress,
            string subject,
            string body,
            bool isBodyHtml,
            string mailgunUser,
            string mailGunApiKey,
            string mailgunDomain,
            string emailFrom,
            MailgunRegion region,
            string? replyTo,
            CancellationToken cancellationToken = default)
        {
            string mailgunHost;

            switch (region)
            {
                case MailgunRegion.US:
                    mailgunHost = "https://api.mailgun.net/v3";
                    break;
                case MailgunRegion.EU:
                    mailgunHost = "https://api.eu.mailgun.net/v3";
                    break;
                default:
                    throw new ArgumentException($"Region {region} is not supported.", nameof(region));
            }

            var form = new List<KeyValuePair<string, string>>
            {
                new("from", emailFrom),
                new("to", toAddress)
            };

            // add reply to address if it specified
            if (!String.IsNullOrWhiteSpace(replyTo))
                form.Add(new("h:Reply-To", replyTo));

            form.Add(new("subject", subject));
            form.Add(new(isBodyHtml ? "html" : "text", body));

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{mailgunHost}/{Uri.EscapeDataString(mailgunDomain)}/messages");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{mailgunUser}:{mailGunApiKey}")));
            request.Content = new FormUrlEncodedContent(form);

            _logger.LogTrace("Sending email to {Email}...", toAddress);
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            string content;
            string? contentType;
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                content = await response.Content.ReadAsStringOrUtf8Async(cancellationToken);
                contentType = response.Content.Headers.ContentType?.MediaType;
                if (!response.IsSuccessStatusCode)
                {
                    var statusCode = (int)response.StatusCode;
                    _logger.LogWarning(
                        "Error sending message to {Email}. StatusCode = {StatusCode}. Response: {Response}",
                        toAddress,
                        statusCode,
                        content);

                    return Response.Failed(new Error((int)ToEmailError(statusCode), content));
                }
            }
            catch (Exception e) when (HttpFailure.IsCommunicationFailure(e, cancellationToken))
            {
                _logger.LogWarning(e, "Error sending message to {Email}", toAddress);

                return Response.Failed(new Error((int)EmailError.Communication, e.Message));
            }

            _logger.LogDebug("Message is successfully sent to {Email}. Response: {Response}", toAddress, content);
            if (contentType != "application/json") return Response.Successful();

            try
            {
                var mgResponse = JsonSerializer.Deserialize<MailGunResponse>(content)!;
                _logger.LogDebug(
                    "MailGun response: message = \"{Message}\", id = \"{Id}\"",
                    mgResponse.Message,
                    mgResponse.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error parsing mailgun response");

                return Response.Failed(new Error((int)EmailError.Communication, "Incorrect mailgun response"));
            }

            return Response.Successful();
        }

        private static EmailError ToEmailError(int statusCode) => statusCode switch
        {
            400 => EmailError.IncorrectRequestData,
            401 or 403 => EmailError.Auth,
            420 or 429 => EmailError.RateLimit,
            >= 500 => EmailError.Communication,
            _ => EmailError.Unknown
        };

        /// <inheritdoc />
        public Task<Response> SendAsync(
            string toAddress,
            string subject,
            string body,
            bool isBodyHtml,
            IEMailExtraParams emailExtraParams,
            CancellationToken cancellationToken = default)
        {
            EmailGuard.AssertToAddress(toAddress);
            EmailGuard.AssertSubject(subject);
            EmailGuard.AssertBody(body);

            if (emailExtraParams == null) throw new ArgumentNullException(nameof(emailExtraParams));

            MailgunEmailExtraParams? mailGunEMailExtraParams = null;
            if (emailExtraParams is MailgunEmailExtraParams @params)
            {
                mailGunEMailExtraParams = @params;
            }
            else
            {
                if (!_mailgunEmailOptions.IgnoreIncorrectExtraParamsType)
                    throw new ArgumentException($"Only {typeof(MailgunEmailExtraParams)} is supported for this sender.", nameof(emailExtraParams));
            }

            var user = mailGunEMailExtraParams?.MailgunUser ?? _mailgunEmailOptions.MailgunUser;
            var apiKey = mailGunEMailExtraParams?.MailgunApiKey ?? _mailgunEmailOptions.MailgunApiKey;
            var domain = mailGunEMailExtraParams?.MailgunDomain ?? _mailgunEmailOptions.MailgunDomain;
            var emailFrom = mailGunEMailExtraParams?.EmailFrom ?? _mailgunEmailOptions.EmailFrom;
            var region = mailGunEMailExtraParams?.MailgunRegion ?? _mailgunEmailOptions.MailgunRegion;

            return SendAsync(
                toAddress,
                subject,
                body,
                isBodyHtml,
                user,
                apiKey,
                domain,
                emailFrom,
                region,
                _mailgunEmailOptions.ReplyTo,
                cancellationToken);
        }
    }
}
