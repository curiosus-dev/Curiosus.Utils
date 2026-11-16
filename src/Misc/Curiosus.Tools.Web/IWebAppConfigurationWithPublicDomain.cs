namespace Curiosus.Tools.Web
{
    /// <summary>
    /// Interface for web app configuration that has public domain which differs from hosting url.
    /// </summary>
    /// <remarks>
    /// <see cref="SiteMap.SiteMapBuilder"/> resolves it from DI: register your implementation yourself.
    /// </remarks>
    public interface IWebAppConfigurationWithPublicDomain
    {
        /// <summary>
        /// Public address to access web app.
        /// </summary>
        public string PublicDomain { get; }
    }
}
