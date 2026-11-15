namespace Curiosus.Tools.Web.DataProtection
{
    /// <summary>
    /// Options of <see cref="DataProtectionServiceCollectionExtensions.AddCuriosusDataProtection"/>.
    /// </summary>
    public class CuriosusDataProtectionOptions
    {
        /// <summary>
        /// Application discriminator of every purpose chain (<c>SetApplicationName</c>). Changing it, even its case,
        /// invalidates auth cookies, antiforgery tokens and Identity tokens already issued: keep the exact value.
        /// </summary>
        public string? ApplicationName { get; set; }

        /// <summary>
        /// Directory of the key ring (<c>PersistKeysToFileSystem</c>). When set, it must exist: a missing directory fails
        /// the host start, it is not replaced by the default. When not set, the framework default location is used
        /// (a user profile folder, or keys in memory only, lost on restart, where there is none, such as a container).
        /// Share it between the instances of the app.
        /// </summary>
        public string? KeyRingPath { get; set; }

        /// <summary>
        /// Don't create or rotate keys automatically: the operator puts them into <see cref="KeyRingPath"/>, which is
        /// required then. With an empty key ring the first <c>Protect</c> fails.
        /// </summary>
        public bool DisableAutomaticKeyGeneration { get; set; }
    }
}
