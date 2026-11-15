using System;
using System.Collections.Generic;
using AngleSharp.Css.Dom;
using Ganss.Xss;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Curiosus.Tools.Web
{
    /// <summary>
    /// Registration of an HTML sanitizer for user-supplied rich text.
    /// </summary>
    public static class HtmlSanitizerServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a singleton <see cref="IHtmlSanitizer"/> (Ganss.Xss) for user-supplied rich text that is rendered later
        /// with <c>Html.Raw</c>. Its output is safe HTML as is: render it without decoding.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Changes the allow-lists, for example adds <c>class</c> to <c>AllowedAttributes</c>.</param>
        /// <remarks>
        /// The allow-lists are the Ganss.Xss defaults plus <c>audio</c>/<c>video</c>/<c>source</c> with
        /// <c>preload</c>/<c>controls</c>/<c>autoplay</c>/<c>muted</c>/<c>loop</c>, the <c>face</c> attribute and the
        /// <c>mailto</c>/<c>sip</c>/<c>tel</c>/<c>skype</c> schemes, minus <c>id</c> and <c>class</c>: user content with them
        /// can shadow the page's global variables (DOM clobbering) or reuse its CSS classes for fake overlays and buttons.
        /// The sanitizer is configured once, so <c>Sanitize</c> is safe to call from several threads.
        /// </remarks>
        public static IServiceCollection AddCuriosusHtmlSanitizer(
            this IServiceCollection services,
            Action<HtmlSanitizerOptions>? configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var options = CreateDefaultOptions();
            configure?.Invoke(options);
            services.TryAddSingleton<IHtmlSanitizer>(new HtmlSanitizer(options));

            return services;
        }

        private static HtmlSanitizerOptions CreateDefaultOptions()
        {
            var options = new HtmlSanitizerOptions
            {
                AllowedTags = Set(HtmlSanitizerDefaults.AllowedTags),
                AllowedAttributes = Set(HtmlSanitizerDefaults.AllowedAttributes),
                AllowedCssProperties = Set(HtmlSanitizerDefaults.AllowedCssProperties),
                AllowedAtRules = new HashSet<CssRuleType>(HtmlSanitizerDefaults.AllowedAtRules),
                AllowedSchemes = Set(HtmlSanitizerDefaults.AllowedSchemes),
                UriAttributes = Set(HtmlSanitizerDefaults.UriAttributes),
                AllowedCssClasses = Set(HtmlSanitizerDefaults.AllowedClasses),
            };

            options.AllowedTags.UnionWith(["audio", "video", "source"]);
            options.AllowedAttributes.UnionWith(["preload", "controls", "autoplay", "muted", "loop", "face"]);
            options.AllowedAttributes.ExceptWith(["id", "class"]);
            options.AllowedSchemes.UnionWith(["mailto", "sip", "tel", "skype"]);

            return options;
        }

        private static ISet<string> Set(IEnumerable<string> values) =>
            new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
    }
}
