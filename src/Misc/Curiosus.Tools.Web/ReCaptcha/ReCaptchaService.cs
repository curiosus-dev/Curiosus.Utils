using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Curiosus.Tools.Web.ReCaptcha
{
    public class ReCaptchaService
    {
        /// <summary>
        /// Name of the <see cref="HttpClient"/> requested from <see cref="IHttpClientFactory"/>.
        /// </summary>
        public const string HttpClientName = "Curiosus.Tools.Web.ReCaptcha";

        private readonly ReCaptchaOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Creates a service that gets <see cref="HttpClient"/> named <see cref="HttpClientName"/> from the factory.
        /// </summary>
        public ReCaptchaService(ReCaptchaOptions options, IHttpClientFactory httpClientFactory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        /// <summary>
        /// Verifies the reCAPTCHA response token.
        /// </summary>
        /// <exception cref="HttpRequestException">The reCAPTCHA API is unavailable or returned an error status code.</exception>
        /// <exception cref="TaskCanceledException">
        /// The reCAPTCHA API did not answer within <see cref="HttpClient.Timeout"/>.
        /// </exception>
        /// <exception cref="JsonException">The reCAPTCHA API returned a body that is not a verification result.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        public async Task<bool> VerifyReCaptchaAsync(string response, CancellationToken cancellationToken = default)
        {
            if (String.IsNullOrWhiteSpace(response))
                return false;

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _options.ReCaptchaServerKey,
                ["response"] = response
            });

            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            using var httpResponse = await httpClient.PostAsync(_options.ReCaptchaApiUrl, content, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var reCaptchaResponse = await httpResponse.Content.ReadFromJsonAsync<ReCaptchaResponse>(cancellationToken);

            return reCaptchaResponse?.success ?? false;
        }
    }
}
