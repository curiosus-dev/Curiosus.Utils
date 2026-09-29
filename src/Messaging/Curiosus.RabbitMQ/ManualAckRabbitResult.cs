using System;
using System.Threading;
using System.Threading.Tasks;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Result of executing RPC via Rabbit that requires manual acknowledge confirmation.
/// </summary>
/// <typeparam name="T">Type of the response.</typeparam>
public class ManualAckRabbitResult<T>
{
    private readonly Func<CancellationToken, Task> _confirmationAction;

    private Task? _confirmationTask;

    /// <summary>
    /// Result of RPC call.
    /// </summary>
    public T Data { get; }

    /// <inheritdoc cref="ManualAckRabbitResult{T}"/>
    internal ManualAckRabbitResult(T data, Func<CancellationToken, Task> confirmationAction)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        _confirmationAction = confirmationAction ?? throw new ArgumentNullException(nameof(confirmationAction));
    }

    /// <summary>
    /// Confirms processing of <see cref="Data"/>.
    /// </summary>
    /// <remarks>
    /// Sends ack to RabbitMQ to remove result from response queue. Only the first call sends the ack, the next ones
    /// return the same task. If the ack fails (for example, it was cancelled), the next call tries again.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task ConfirmAcknowledgeAsync(CancellationToken cancellationToken = default)
    {
        var confirmation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var existing = Interlocked.CompareExchange(ref _confirmationTask, confirmation.Task, null);
        if (existing != null) return existing;

        _ = ConfirmAsync(confirmation, cancellationToken);

        return confirmation.Task;
    }

    private async Task ConfirmAsync(TaskCompletionSource confirmation, CancellationToken cancellationToken)
    {
        try
        {
            await _confirmationAction.Invoke(cancellationToken);
            confirmation.SetResult();
        }
        catch (Exception e)
        {
            Volatile.Write(ref _confirmationTask, null);
            confirmation.SetException(e);
        }
    }
}
