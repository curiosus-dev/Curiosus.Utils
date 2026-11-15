using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Curiosus.Configuration;
using Curiosus.Tools;
using Microsoft.Extensions.Logging;
using System;

namespace Curiosus.SMS.Iqsms;

/// <inheritdoc cref="IIqsmsSender"/>
public class IqsmsSender : IIqsmsSender
{
    /// <summary>
    /// Name of the <see cref="HttpClient"/> requested from <see cref="IHttpClientFactory"/>.
    /// </summary>
    public const string HttpClientName = "Curiosus.SMS.Iqsms";

    private const string SendUrl = "https://api.iqsms.ru/messages/v2/send.json";

    private readonly ILogger<IqsmsSender> _logger;
    private readonly IqsmsOptions         _options;
    private readonly IHttpClientFactory   _httpClientFactory;

    /// <summary>
    /// Creates a sender that gets <see cref="HttpClient"/> named <see cref="HttpClientName"/> from the factory.
    /// </summary>
    public IqsmsSender(ILogger<IqsmsSender> logger, IqsmsOptions options, IHttpClientFactory httpClientFactory)
    {
        _logger = logger   ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        options.AssertValid();
    }

    /// <inheritdoc />
    public Task<Response<SmsSentResult>> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentNullException(nameof(phoneNumber));
        if (String.IsNullOrWhiteSpace(message)) throw new ArgumentNullException(nameof(message));

        return SendSmsAsync(phoneNumber, message, _options.Login, _options.Password, _options.Sender, cancellationToken);
    }

    /// <inheritdoc />
    public Task<Response<SmsSentResult>> SendSmsAsync(string phoneNumber, string message, ISmsExtraParams extraParams, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentNullException(nameof(phoneNumber));
        if (String.IsNullOrWhiteSpace(message)) throw new ArgumentNullException(nameof(message));
        if (extraParams == null) throw new ArgumentNullException(nameof(extraParams));
        if (!(extraParams is IqsmsExtraParams iqsmsExtraParams)) throw new ArgumentException($"Only {typeof(IqsmsExtraParams)} is supported.", nameof(extraParams));

        var login = iqsmsExtraParams.Login           ?? _options.Login;
        var password = iqsmsExtraParams.Password     ?? _options.Password;
        var senderName = iqsmsExtraParams.SenderName ?? _options.Sender;

        return SendSmsAsync(phoneNumber, message, login, password, senderName, cancellationToken);
    }

    private async Task<Response<SmsSentResult>> SendSmsAsync(
        string phoneNumber,
        string message,
        string login,
        string password,
        string? senderName,
        CancellationToken cancellationToken)
    {
        // transform to +71234567890
        if (_options.AutoTransformPhoneNumber)
        {
            phoneNumber = phoneNumber[0] switch
            {
                '8' => "+7" + phoneNumber.Substring(1),
                '7' => "+"  + phoneNumber,
                _   => phoneNumber,
            };
        }

        var maskedPhone = phoneNumber.MaskForLog();

        // credentials, phone and text go in the JSON body: proxies, gateways and access logs record URLs
        var request = new IqsmsSendRequest(
            login,
            password,
            [new IqsmsSendMessage(ClientId: "1", Phone: phoneNumber, Text: message, Sender: String.IsNullOrWhiteSpace(senderName) ? null : senderName)]);

        _logger.LogInformation("Sending SMS to {PhoneNumber}", maskedPhone);

        var response = await ExecuteAsync(request, cancellationToken);
        var content = response.Content ?? "empty content";

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return Fail(SmsError.Auth, "Authorization failed", content, maskedPhone);

        if (response.ErrorMessage != null)
            return Fail(SmsError.Communication, response.ErrorMessage, content, maskedPhone);

        IqsmsSendResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<IqsmsSendResponse>(content, JsonOptions);
        }
        catch (JsonException)
        {
            parsed = null;
        }

        if (parsed == null)
            return Fail(SmsError.Unknown, "Unexpected response content", content, maskedPhone);

        if (!String.Equals(parsed.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            var description = parsed.Description ?? parsed.Status ?? "error";
            var error = description.Contains("auth", StringComparison.OrdinalIgnoreCase) ? SmsError.Auth : SmsError.Unknown;
            return Fail(error, description, content, maskedPhone);
        }

        var messageStatus = parsed.Messages?.FirstOrDefault()?.Status?.Trim();
        if (!String.Equals(messageStatus, "accepted", StringComparison.OrdinalIgnoreCase))
        {
            var error = messageStatus?.ToLowerInvariant() switch
            {
                "not enough balance" or "not enough credits" => SmsError.NoMoney,
                "invalid mobile phone" => SmsError.DeliveryError,
                _ => SmsError.Unknown,
            };
            return Fail(error, messageStatus ?? "no message status", content, maskedPhone);
        }

        _logger.LogInformation("SMS to {PhoneNumber} was accepted", maskedPhone);
        return Response.Successful(new SmsSentResult(null, null, content));
    }

    private Response<SmsSentResult> Fail(SmsError error, string description, string content, string maskedPhone)
    {
        _logger.LogWarning(
            "Sending SMS to {PhoneNumber} failed: {Error} ({Description})", maskedPhone, error, description);
        return Response.Failed(new Error((int)error, description), new SmsSentResult(null, null, content));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record IqsmsSendRequest(string Login, string Password, IReadOnlyList<IqsmsSendMessage> Messages);

    private sealed record IqsmsSendMessage(string ClientId, string Phone, string Text, string? Sender);

    private sealed record IqsmsSendResponse(string? Status, string? Description, IReadOnlyList<IqsmsMessageStatus>? Messages);

    private sealed record IqsmsMessageStatus(string? ClientId, string? SmscId, string? Status);

    private sealed record IqsmsHttpResponse(HttpStatusCode StatusCode, string? Content, string? ErrorMessage);

    private async Task<IqsmsHttpResponse> ExecuteAsync(IqsmsSendRequest request, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        try
        {
            using var content = JsonContent.Create(request, options: JsonOptions);
            using var response = await httpClient.PostAsync(SendUrl, content, cancellationToken);
            var body = await response.Content.ReadAsStringOrUtf8Async(cancellationToken);
            var errorMessage = response.IsSuccessStatusCode
                ? null
                : $"Request failed with status code {(int)response.StatusCode} ({response.ReasonPhrase})";

            return new IqsmsHttpResponse(response.StatusCode, body, errorMessage);
        }
        catch (Exception e) when (HttpFailure.IsCommunicationFailure(e, cancellationToken))
        {
            return new IqsmsHttpResponse(0, null, e.Message);
        }
    }
}
