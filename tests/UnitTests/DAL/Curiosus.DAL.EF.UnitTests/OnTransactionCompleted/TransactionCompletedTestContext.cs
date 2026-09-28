using Microsoft.EntityFrameworkCore;

namespace Curiosus.DAL.EF.UnitTests.OnTransactionCompleted
{
    /// <summary>
    /// Data context for <see cref="OnTransactionCompletedEventTests"/>.
    /// </summary>
    public class TransactionCompletedTestContext : CuriosusDataContext<TransactionCompletedTestContext>
    {
        public TransactionCompletedTestContext(DbContextOptions<TransactionCompletedTestContext> options) : base(options)
        {
        }
    }
}
