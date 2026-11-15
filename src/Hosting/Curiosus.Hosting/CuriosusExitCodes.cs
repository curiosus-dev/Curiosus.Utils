namespace Curiosus.Hosting
{
    /// <summary>
    /// Exit codes returned by <see cref="CuriosusApp.RunAsync(Microsoft.Extensions.Hosting.IHost, System.Threading.CancellationToken)"/>.
    /// </summary>
    public static class CuriosusExitCodes
    {
        /// <summary>
        /// The app completed successfully.
        /// </summary>
        public const int Success = 0;

        /// <summary>
        /// Options failed validation when the host started.
        /// </summary>
        public const int InvalidOptions = 1;

        /// <summary>
        /// An unhandled exception stopped the app.
        /// </summary>
        public const int UnhandledException = 2;

        /// <summary>
        /// The app was cancelled.
        /// </summary>
        public const int Cancellation = 3;
    }
}
