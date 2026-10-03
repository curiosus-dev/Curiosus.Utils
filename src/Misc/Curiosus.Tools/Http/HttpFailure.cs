using System;
using System.Net.Http;
using System.Threading;

namespace Curiosus.Tools
{
    /// <summary>
    /// Classifies exceptions thrown by <see cref="HttpClient"/>.
    /// </summary>
    public static class HttpFailure
    {
        /// <summary>
        /// Returns <see langword="true"/> when the remote call itself failed: a network error or an
        /// <see cref="HttpClient.Timeout"/>. Cancellation requested by <paramref name="cancellationToken"/> is not a failure
        /// of the call and returns <see langword="false"/>, so it propagates to the caller.
        /// </summary>
        public static bool IsCommunicationFailure(Exception exception, CancellationToken cancellationToken) =>
            exception is HttpRequestException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
    }
}
