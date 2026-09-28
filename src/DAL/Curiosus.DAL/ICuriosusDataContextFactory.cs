namespace Curiosus.DAL
{
    /// <summary>
    /// Data context factory (for <see cref="ICuriosusDataContext"/>) with typed returned value.
    /// </summary>
    public interface ICuriosusDataContextFactory<out TContext> : ICuriosusDataContextFactory
        where TContext : ICuriosusDataContext
    {
        /// <summary>
        /// Creates context <see cref="TContext"/> for data access.
        /// </summary>
        /// <returns>New instance of data context</returns>
        new TContext CreateContext(bool isLoggingEnabled = false);
    }
    
    /// <summary>
    /// Data context factory (for <see cref="ICuriosusDataContext"/>) with typed returned value.
    /// </summary>
    public interface ICuriosusReadOnlyDataContextFactory<out TContext> : ICuriosusReadOnlyDataContextFactory
        where TContext  : ICuriosusReadOnlyDataContext
    {
        /// <summary>
        /// Creates context <see cref="TContext"/> for read only data access (if no connection string found for read only access, main connection will be used).
        /// </summary>
        /// <returns>New instance of data context</returns>
        new TContext CreateReadOnlyContext(bool isLoggingEnabled = false);
    }

    /// <summary>
    /// Data context factory (for <see cref="ICuriosusDataContext"/>).
    /// </summary>
    public interface ICuriosusDataContextFactory
    {
        /// <summary>
        /// Creates context <see cref="ICuriosusDataContext"/> for data access.
        /// </summary>
        /// <returns>New instance of data context</returns>
        ICuriosusDataContext CreateContext(bool isLoggingEnabled = false);
    }
    
    /// <summary>
    /// Data context factory (for <see cref="ICuriosusReadOnlyDataContext"/>).
    /// </summary>
    public interface ICuriosusReadOnlyDataContextFactory
    {
        /// <summary>
        /// Creates context <see cref="ICuriosusReadOnlyDataContext"/> for read only data access (if no connection string found for read only access, main connection will be used).
        /// </summary>
        /// <returns>New instance of data context</returns>
        ICuriosusReadOnlyDataContext CreateReadOnlyContext(bool isLoggingEnabled = false);
    }
}