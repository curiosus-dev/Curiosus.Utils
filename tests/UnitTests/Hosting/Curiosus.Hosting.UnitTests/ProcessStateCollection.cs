using Xunit;

namespace Curiosus.Hosting.UnitTests
{
    /// <summary>
    /// Tests that change environment variables or the process culture.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ProcessStateCollection
    {
        public const string Name = "Process-wide state";
    }
}
