using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Curiosus.RabbitMQ;

/// <summary>
/// JSON settings of the messages the package sends and receives.
/// </summary>
public static class RabbitMQJson
{
    /// <summary>
    /// Default JSON options of messages: those of the RPC client since 2.0.0.
    /// </summary>
    /// <remarks>
    /// Mimics Newtonsoft.Json defaults (used before 2.0.0) to stay wire-compatible with existing peers: case-insensitive
    /// property names, public fields, numbers from strings and unescaped non-ASCII characters. Enums are written as
    /// numbers. The instance is read-only; to customize, copy it: <c>new JsonSerializerOptions(RabbitMQJson.DefaultOptions)</c>.
    /// </remarks>
    public static JsonSerializerOptions DefaultOptions { get; } = CreateDefaultOptions();

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
