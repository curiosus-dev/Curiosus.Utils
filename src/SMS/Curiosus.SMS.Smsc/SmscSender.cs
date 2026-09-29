using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.Configuration;
using Curiosus.Tools;
using Microsoft.Extensions.Logging;

namespace Curiosus.SMS.Smsc
{
    /// <inheritdoc />
    public class SmscSender : ISmscSender
    {
        /// <summary>
        /// Name of the <see cref="HttpClient"/> requested from <see cref="IHttpClientFactory"/>.
        /// </summary>
        public const string HttpClientName = "Curiosus.SMS.Smsc";

        private const string SendUrl = "https://smsc.ru/sys/send.php";

        internal static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

        internal static readonly JsonSerializerOptions ResultJsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly ILogger _logger;
        private readonly SmscOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Creates a sender that gets <see cref="HttpClient"/> named <see cref="HttpClientName"/> from the factory.
        /// </summary>
        public SmscSender(ILogger<SmscSender> logger, SmscOptions options, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            options.AssertValid();
        }

        /// <inheritdoc />
        public Task<Response<SmsSentResult>> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentNullException(nameof(phoneNumber));
            if (String.IsNullOrWhiteSpace(message)) throw new ArgumentNullException(nameof(message));

            return SendSmsAsync(phoneNumber, message, _options.SmscLogin, _options.SmscPassword, _options.SmscSender, 0, cancellationToken);
        }

        private async Task<Response<SmsSentResult>> SendSmsAsync(
            string phoneNumber,
            string message,
            string smscLogin,
            string smscPassword,
            string? senderName,
            int retriesCount = 0,
            CancellationToken cancellationToken = default)
        {
            var query = new List<KeyValuePair<string, string>>
            {
                new("login", smscLogin),
                new("psw", smscPassword)
            };

            if (!String.IsNullOrWhiteSpace(senderName))
            {
                query.Add(new("sender", senderName));
            }

            query.Add(new("phones", phoneNumber));
            query.Add(new("mes", message));

            query.Add(new("cost", "2")); // отправить и вернуть стоимость
            query.Add(new("fmt", "3")); // результат в json

            _logger.LogInformation($"Отправляем sms на номер {phoneNumber}...");

            // execute
            var response = await ExecuteAsync(SendUrl + "?" + ToQueryString(query), cancellationToken);

            string resultJson;
            decimal? messageCost = null;
            if (response.IsSuccessful)
            {
                resultJson = JsonSerializer.Serialize(response.Data, ResultJsonOptions);
                _logger.LogDebug(resultJson);

                // has sent error
                var errorCode = response.Data?.error_code ?? 0;
                var error = response.Data?.Error;
                if (errorCode != 0 || error != null)
                {
                    // if we got message denied error (6) and it is our first attempt, let's remove sender name and try again
                    // because SMSC can block SMS to Megafon and Tele2 with specified sender name when there is sender name is not agreed
                    if (errorCode == 6 && retriesCount == 0)
                    {
                        _logger.LogWarning("Отправка SMS на номер {PhoneNumber} заблокирована. Попробуем повторно отправить без указания имени отправтиеля (текущее имя отправителя = \"{SenderName}\")", phoneNumber, senderName);
                        return await SendSmsAsync(phoneNumber, message, smscLogin, smscPassword, null, 1, cancellationToken);
                    }

                    var errorMessage = $"Ошибка при отправке sms на номер {phoneNumber} (error_code = {errorCode}, error = \"{error}\")";
                    _logger.LogWarning($"Ошибка при отправке sms на номер {phoneNumber} (error_code = {errorCode}, error = \"{error}\")");

                    // check error code
                    switch (errorCode)
                    {
                        case 1: // Ошибка в параметрах.
                        case 2: // Неверный логин или пароль.
                            return Response.Failed(new Error((int) SmsError.Auth, errorMessage), new SmsSentResult(null, null, resultJson));
                        
                        case 3: // Недостаточно средств на счёте Клиента.
                            return Response.Failed(new Error((int) SmsError.NoMoney, errorMessage), new SmsSentResult(null, null, resultJson));

                        case 4: // IP-адрес временно заблокирован из-за частых ошибок в запросах. 
                        case 9: // Более 15 параллельных запросов под одним логином с разных подключений.
                            return Response.Failed(new Error((int) SmsError.RateLimit, errorMessage), new SmsSentResult(null, null, resultJson));
                        
                        case 8: // can't to deliver
                            return Response.Failed(new Error((int)SmsError.DeliveryError, errorMessage), new SmsSentResult(null, null, resultJson));

                        default:
                            return Response.Failed(new Error((int) SmsError.Unknown, errorMessage), new SmsSentResult(null, null, resultJson));
                    }
                }
            }
            // has HTTP error
            else
            {
                var data = new SmscResponseData
                {
                    error_code = -1
                };
                data.Error = response.ErrorException != null
                    ? $"{response.ErrorException.GetType().Name}: {response.ErrorException.Message}"
                    : $"{response.StatusCode}, {response.ReasonPhrase}";

                resultJson = JsonSerializer.Serialize(data, ResultJsonOptions);

                _logger.LogWarning(
                    $"Ошибка при отправке sms на номер {phoneNumber} (error = \"{data.Error}\")");

                return Response.Failed(new Error((int)SmsError.Unknown, data.Error), new SmsSentResult(null, null, resultJson));
            }

            // successfully sent
            if (response.Data?.Cost != null)
                messageCost = response.Data.Cost;

            _logger.LogInformation($"Успешно отправили sms на номер {phoneNumber}");
            
            int? sentSmsCount = null;
            if (response.IsSuccessful && response.Data != null! && response.Data.Count > 0)
                sentSmsCount = response.Data.Count.Value;

            var result = new SmsSentResult(sentSmsCount, messageCost, resultJson);
            return Response.Successful(result);
        }

        private sealed record SmscHttpResponse(
            bool IsSuccessful,
            SmscResponseData? Data,
            int StatusCode,
            string? ReasonPhrase,
            Exception? ErrorException);

        private async Task<SmscHttpResponse> ExecuteAsync(string requestUri, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return new SmscHttpResponse(false, null, (int)response.StatusCode, response.ReasonPhrase, null);

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var data = JsonSerializer.Deserialize<SmscResponseData>(content, ResponseJsonOptions);
                return new SmscHttpResponse(true, data, (int)response.StatusCode, response.ReasonPhrase, null);
            }
            catch (Exception e) when (e is HttpRequestException or JsonException
                                          || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
            {
                return new SmscHttpResponse(false, null, 0, null, e);
            }
        }

        private static string ToQueryString(IEnumerable<KeyValuePair<string, string>> parameters) =>
            String.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        /// <inheritdoc />
        public Task<Response<SmsSentResult>> SendSmsAsync(string phoneNumber, string message, ISmsExtraParams extraParams, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentNullException(nameof(phoneNumber));
            if (String.IsNullOrWhiteSpace(message)) throw new ArgumentNullException(nameof(message));
            if (extraParams == null) throw new ArgumentNullException(nameof(extraParams));
            if (!(extraParams is SmscExtraParams smscExtraParams)) throw new ArgumentException($"Only {typeof(SmscExtraParams)} is supported.", nameof(SmscExtraParams));

            var login = smscExtraParams.SmscLogin ?? _options.SmscLogin;
            var password = smscExtraParams.SmscPassword ?? _options.SmscPassword;
            var senderName = smscExtraParams.SenderName ?? _options.SmscSender;

            return SendSmsAsync(phoneNumber, message, login, password, senderName, 0, cancellationToken);
        }
    }
}
