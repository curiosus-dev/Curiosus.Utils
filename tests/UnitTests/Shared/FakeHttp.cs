#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Curiosus.UnitTests.Shared
{
    /// <summary>
    /// Request captured by <see cref="FakeHttpMessageHandler"/>, with the body read before the request is disposed.
    /// </summary>
    public sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        IReadOnlyDictionary<string, string> Headers,
        string? ContentType,
        string? Body);

    /// <summary>
    /// Returns a canned response for every request and records what was sent.
    /// </summary>
    public sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public List<CapturedRequest> Requests { get; } = new();

        public FakeHttpMessageHandler Respond(
            HttpStatusCode statusCode,
            string content = "",
            string mediaType = "application/json")
        {
            _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, mediaType)
            });
            return this;
        }

        public FakeHttpMessageHandler Throw(Exception exception)
        {
            _responses.Enqueue(_ => throw exception);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
                headers[header.Key] = String.Join(",", header.Value);

            string? body = null;
            string? contentType = null;
            if (request.Content != null)
            {
                body = await request.Content.ReadAsStringAsync(cancellationToken);
                contentType = request.Content.Headers.ContentType?.MediaType;
            }

            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!, headers, contentType, body));

            if (_responses.Count == 0)
                throw new InvalidOperationException($"No response configured for {request.Method} {request.RequestUri}");

            return _responses.Dequeue()(request);
        }
    }

    /// <summary>
    /// <see cref="IHttpClientFactory"/> that creates clients over one <see cref="FakeHttpMessageHandler"/>.
    /// </summary>
    public sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public List<string> CreatedClientNames { get; } = new();

        public HttpClient CreateClient(string name)
        {
            CreatedClientNames.Add(name);
            return new HttpClient(_handler, disposeHandler: false);
        }
    }
}
