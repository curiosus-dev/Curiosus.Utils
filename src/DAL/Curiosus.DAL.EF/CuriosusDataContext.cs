using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Curiosus.DAL.EF
{
    /// <summary>
    /// Base read-write data context at Curiosus projects
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class CuriosusDataContext<T> : CuriosusReadOnlyDataContext<T>, ICuriosusDataContext  where T: DbContext
    {
        /// <summary>
        /// Created transaction for this context.
        /// </summary>
        private CuriosusDbTransaction? _transaction;

        /// <inheritdoc />
        public event Action? OnTransactionCompleted;

        /// <summary>
        /// Notifies when transaction successfully completed.
        /// </summary>
        public event Func<CancellationToken, Task>? OnTransactionCompletedAsync;

        /// <inheritdoc />
        protected CuriosusDataContext(DbContextOptions<T> options) : base(options)
        {
        }

        /// <inheritdoc />
        public ICuriosusDataContextTransaction BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            var transaction = base.Database.BeginTransaction(isolationLevel);

            var curiosusTransaction = new CuriosusDbTransaction(transaction);
            _transaction = curiosusTransaction;
            _transaction.OnTransactionCompleted += () => OnTransactionCompleted?.Invoke();
            _transaction.OnTransactionCompletedAsync += ct => OnTransactionCompletedAsync != null
                ? OnTransactionCompletedAsync.Invoke(ct)
                : Task.CompletedTask;

            return curiosusTransaction;
        }

        /// <inheritdoc />
        public async Task<ICuriosusDataContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
        {
            var transaction = await base.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

            var curiosusTransaction = new CuriosusDbTransaction(transaction);
            _transaction = curiosusTransaction;
            _transaction.OnTransactionCompleted += () => OnTransactionCompleted?.Invoke();
            _transaction.OnTransactionCompletedAsync += ct => OnTransactionCompletedAsync != null
                ? OnTransactionCompletedAsync.Invoke(ct)
                : Task.CompletedTask;

            return curiosusTransaction;
        }

        /// <inheritdoc />
        public void UseTransaction(DbTransaction transaction)
        {
            var efTransaction = Database.UseTransaction(transaction);

            var curiosusTransaction = new CuriosusDbTransaction(efTransaction);
            _transaction = curiosusTransaction;
            _transaction.OnTransactionCompleted += () => OnTransactionCompleted?.Invoke();
        }

        /// <inheritdoc />
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            var result = base.SaveChanges(acceptAllChangesOnSuccess);

            NotifyAboutTransaction();

            return result;
        }

        /// <summary>
        /// Notifies subscribers about successfully completed transaction.
        /// </summary>
        private void NotifyAboutTransaction()
        {
            // if no transaction and there is AutoTransactions Enabled let's invoke event
            // in other cases event will be invoked on created transaction
            if (_transaction == null && Database.AutoTransactionsEnabled)
            {
                OnTransactionCompleted?.Invoke();

                if (OnTransactionCompletedAsync != null)
                {
                    OnTransactionCompletedAsync.Invoke(CancellationToken.None).GetAwaiter().GetResult();
                }

                // reset subscribers
                OnTransactionCompleted = null;
                OnTransactionCompletedAsync = null;
            }
        }

        /// <inheritdoc cref="ICuriosusDataContext.SaveChangesAsync(bool,System.Threading.CancellationToken)" />
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new CancellationToken())
        {
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            await NotifyAboutTransactionAsync(cancellationToken);

            return result;
        }

        /// <summary>
        /// Notifies subscribers about successfully completed transaction.
        /// </summary>
        private async ValueTask NotifyAboutTransactionAsync(CancellationToken cancellationToken = default)
        {
            // if no transaction and there is AutoTransactions Enabled let's invoke event
            // in other cases event will be invoked on created transaction
            if (_transaction == null && Database.AutoTransactionsEnabled)
            {
                OnTransactionCompleted?.Invoke();

                if (OnTransactionCompletedAsync != null)
                {
                    await OnTransactionCompletedAsync.Invoke(cancellationToken);
                }

                // reset subscribers
                OnTransactionCompleted = null;
                OnTransactionCompletedAsync = null;
            }
        }
    }
}
