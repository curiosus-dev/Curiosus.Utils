using System;
using System.Net.Mime;
using System.Text.Json;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Default <see cref="IRabbitMQMessageSerializer"/>: System.Text.Json with <see cref="RabbitMQJson.DefaultOptions"/>.
/// </summary>
internal sealed class RabbitMQJsonMessageSerializer : IRabbitMQMessageSerializer
{
    public string ContentType => MediaTypeNames.Application.Json;

    public ReadOnlyMemory<byte> Serialize<T>(T message)
        => JsonSerializer.SerializeToUtf8Bytes(message, RabbitMQJson.DefaultOptions);

    public T Deserialize<T>(ReadOnlyMemory<byte> body, string? contentType)
    {
        if (contentType != null && !IsJson(contentType))
        {
            throw new NotSupportedException(
                $"Can't deserialize a message with content type \"{contentType}\": only JSON is supported");
        }

        return JsonSerializer.Deserialize<T>(body.Span, RabbitMQJson.DefaultOptions)!;
    }

    // "application/json", optionally with parameters: "application/json; charset=utf-8"
    private static bool IsJson(string contentType)
    {
        var mediaType = contentType.AsSpan();
        var parametersStart = mediaType.IndexOf(';');
        if (parametersStart >= 0) mediaType = mediaType[..parametersStart];

        return mediaType.Trim().Equals(MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase);
    }
}
