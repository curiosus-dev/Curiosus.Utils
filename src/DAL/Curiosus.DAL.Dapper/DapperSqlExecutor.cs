using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace Curiosus.DAL.Dapper
{
    /// <inheritdoc />
    public class DapperSqlExecutor : ISqlExecutor
    {
        /// <inheritdoc />
        public void Execute(
            ICuriosusDataContext context,
            string sqlTemplate,
            IDictionary<string, object>? parameters = null,
            bool ignoreNulls = false,
            int? commandTimeoutSec = null)
        {
            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            var connection = context.Connection;
            connection.Execute(sqlTemplate, dynamicParams, commandTimeout: commandTimeoutSec);
        }

        /// <inheritdoc />
        public Task ExecuteAsync(
            ICuriosusDataContext context,
            string sqlTemplate,
            IDictionary<string, object>? parameters = null,
            bool ignoreNulls = false,
            int? commandTimeoutSec = null,
            CancellationToken cancellationToken = default)
        {
            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            var command = new CommandDefinition(sqlTemplate, dynamicParams, commandTimeout: commandTimeoutSec, cancellationToken: cancellationToken);
            
            var connection = context.Connection;
            return connection.ExecuteAsync(command);
        }

        /// <inheritdoc />
        public T QuerySingleOrDefault<T>(
            ICuriosusReadOnlyDataContext context,
            string sqlTemplate,
            IDictionary<string, object>? parameters = null,
            bool ignoreNulls = false,
            int? commandTimeoutSec = null)
        {
            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            var connection = context.Connection;
            return connection.QuerySingleOrDefault<T>(sqlTemplate, dynamicParams, commandTimeout: commandTimeoutSec);
        }

        /// <inheritdoc />
        public Task<T> QuerySingleOrDefaultAsync<T>(
            ICuriosusReadOnlyDataContext context,
            string sqlTemplate,
            IDictionary<string, object>? parameters = null,
            bool ignoreNulls = false,
            int? commandTimeoutSec = null,
            CancellationToken cancellationToken = default)
        {
            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            var command = new CommandDefinition(sqlTemplate, dynamicParams, commandTimeout: commandTimeoutSec, cancellationToken: cancellationToken);
            
            var connection = context.Connection;
            return connection.QuerySingleOrDefaultAsync<T>(command);
        }

        /// <inheritdoc />
        public IEnumerable<T> QueryMany<T>(
            ICuriosusReadOnlyDataContext context,
            string sqlTemplate,
            IDictionary<string, object>? parameters = null,
            bool ignoreNulls = false,
            int? commandTimeoutSec = null)
        {
            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            var connection = context.Connection;
            var result = connection.Query<T>(sqlTemplate, dynamicParams, commandTimeout: commandTimeoutSec);

            return result;
        }

        /// <inheritdoc />
        public Task<IEnumerable<T>> QueryManyAsync<T>(
            ICuriosusReadOnlyDataContext context,
            string sqlTemplate,
            IDictionary<string, object>? parameters = null,
            bool ignoreNulls = true,
            int? commandTimeoutSec = null,
            CancellationToken cancellationToken = default)
        {
            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            var commandDefinition = new CommandDefinition(sqlTemplate, dynamicParams, commandTimeout: commandTimeoutSec, cancellationToken: cancellationToken);
            
            var connection = context.Connection;
            return connection.QueryAsync<T>(commandDefinition);
        }
        
        /// <inheritdoc />
        public Task<IEnumerable<T>> QueryManyAsync<T>(
            ICuriosusReadOnlyDataContext context,
            string sqlTemplate,
            object? parameters,
            int? commandTimeoutSec = null,
            CancellationToken cancellationToken = default)
        {
            var connection = context.Connection;

            var commandDefinition = new CommandDefinition(sqlTemplate, parameters, commandTimeout: commandTimeoutSec, cancellationToken: cancellationToken);
            
            return connection.QueryAsync<T>(commandDefinition);
        }

        /// <inheritdoc />
        public T ExecuteStoredProcedure<T>(
            ICuriosusDataContext context,
            string procedureName,
            IDictionary<string, object>? parameters = null,
            int? commandTimeoutSec = null,
            bool ignoreNulls = true)
        {
            var connection = context.Connection;

            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            return connection.QuerySingleOrDefault<T>(
                procedureName,
                dynamicParams,
                commandTimeout: commandTimeoutSec,
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public Task<T> ExecuteStoredProcedureAsync<T>(
            ICuriosusDataContext context,
            string procedureName,
            IDictionary<string, object>? parameters = null,
            int? commandTimeoutSec = null,
            bool ignoreNulls = true,
            CancellationToken cancellationToken = default)
        {
            var connection = context.Connection;

            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            var commandDefinition = new CommandDefinition(
                procedureName,
                dynamicParams,
                commandType: CommandType.StoredProcedure,
                commandTimeout: commandTimeoutSec,
                cancellationToken: cancellationToken);
            
            return connection.QuerySingleOrDefaultAsync<T>(commandDefinition);
        }

        /// <inheritdoc />
        public void ExecuteStoredProcedure(
            ICuriosusDataContext context,
            string procedureName,
            IDictionary<string, object>? parameters = null,
            int? commandTimeoutSec = null,
            bool ignoreNulls = true)
        {
            var connection = context.Connection;

            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            connection.QuerySingleOrDefault(
                procedureName,
                dynamicParams,
                commandTimeout: commandTimeoutSec,
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public Task ExecuteStoredProcedureAsync(
            ICuriosusDataContext context,
            string procedureName,
            IDictionary<string, object>? parameters = null,
            int? commandTimeoutSec = null,
            bool ignoreNulls = true,
            CancellationToken cancellationToken = default)
        {
            var connection = context.Connection;

            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            var commandDefinition = new CommandDefinition(
                procedureName,
                dynamicParams,
                commandTimeout: commandTimeoutSec,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken);
            
            return connection.QuerySingleOrDefaultAsync(commandDefinition);
        }

        /// <inheritdoc />
        public IEnumerable<T> ExecuteStoredProcedure<T>(
            ICuriosusReadOnlyDataContext context,
            string procedureName,
            IDictionary<string, object>? parameters = null,
            int? commandTimeoutSec = null,
            bool ignoreNulls = true)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var connection = context.Connection;

            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            return connection.Query<T>(
                procedureName,
                dynamicParams,
                commandTimeout: commandTimeoutSec,
                commandType: CommandType.StoredProcedure);
        }

        /// <inheritdoc />
        public Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(
            ICuriosusReadOnlyDataContext context,
            string procedureName,
            IDictionary<string, object>? parameters = null,
            int? commandTimeoutSec = null,
            bool ignoreNulls = true,
            CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var connection = context.Connection;

            var dynamicParams = parameters?.ConvertToDynamicParameters(ignoreNulls);

            var commandDefinition = new CommandDefinition(
                procedureName,
                dynamicParams,
                commandTimeout: commandTimeoutSec,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken);

            return connection.QueryAsync<T>(commandDefinition);
        }
    }
}