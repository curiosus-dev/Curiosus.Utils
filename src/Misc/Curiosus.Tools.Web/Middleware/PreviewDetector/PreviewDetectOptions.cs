using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.Middleware
{
    /// <summary>
    /// Options for the <see cref="PreviewDetectMiddleware"/>.
    /// </summary>
    public class PreviewDetectOptions
    {
        /// <summary>
        /// The path to redirect to if a Preview request is detected, for example <c>/preview</c>. Required.
        /// </summary>
        public PathString RedirectPath { get; set; }
    }

    /// <summary>
    /// Validates <see cref="PreviewDetectOptions"/>: <see cref="PreviewDetectOptions.RedirectPath"/> is set.
    /// </summary>
    public class PreviewDetectOptionsValidator : IValidateOptions<PreviewDetectOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, PreviewDetectOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var builder = new ValidateOptionsResultBuilder();
            if (String.IsNullOrWhiteSpace(options.RedirectPath.Value))
                builder.AddError("can't be empty", nameof(options.RedirectPath));

            return builder.Build();
        }
    }
}
