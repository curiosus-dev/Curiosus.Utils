using System;
using Microsoft.Extensions.Options;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Validates <see cref="RabbitMQOptions"/>: an absolute <c>amqp</c>/<c>amqps</c> URI with a host and user info that
/// RabbitMQ.Client can parse, and a positive <see cref="RabbitMQOptions.PublishTimeout"/>.
/// </summary>
/// <remarks>
/// Error messages never contain the URI: it carries the password.
/// </remarks>
public class RabbitMQOptionsValidator : IValidateOptions<RabbitMQOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RabbitMQOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new ValidateOptionsResultBuilder();
        ValidateUri(options.ConnectionUri, builder);

        if (options.PublishTimeout <= TimeSpan.Zero)
            builder.AddError("must be positive", nameof(options.PublishTimeout));

        return builder.Build();
    }

    private static void ValidateUri(Uri? uri, ValidateOptionsResultBuilder builder)
    {
        // a missing URI is reported by [Required]
        if (uri == null) return;

        const string property = nameof(RabbitMQOptions.ConnectionUri);
        if (!uri.IsAbsoluteUri)
        {
            builder.AddError("must be an absolute amqp:// or amqps:// URI", property);
            return;
        }

        if (!RabbitConnectionFactoryBuilder.IsSupportedScheme(uri))
        {
            builder.AddError("must use the amqp or amqps scheme", property);
            return;
        }

        if (String.IsNullOrEmpty(uri.Host))
            builder.AddError("must have a host", property);

        // RabbitMQ.Client splits the user info by ':' and throws with the whole user info, password included
        if (uri.UserInfo.Split(':').Length > 2)
            builder.AddError("has an unescaped ':' in the user name or the password: escape it as %3A", property);

        // C# keeps at least the "/" segment: a second one is the virtual host, more ones fail in RabbitMQ.Client
        if (uri.Segments.Length > 2)
            builder.AddError("must have one path segment at most, the virtual host: escape '/' in it as %2F", property);
    }
}
