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
    private readonly Func<Task> _confirmationAction;

    private Task? _confirmationTask;

    /// <summary>
    /// Result of RPC call.
    /// </summary>
    public T Data { get; }

    /// <inheritdoc cref="ManualAckRabbitResult{T}"/>
    internal ManualAckRabbitResult(T data, Func<Task> confirmationAction)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        _confirmationAction = confirmationAction ?? throw new ArgumentNullException(nameof(confirmationAction));
    }

    /// <summary>
    /// Confirms processing of <see cref="Data"/>.
    /// </summary>
    /// <remarks>
    /// Sends ack to RabbitMQ to remove result from response queue. Concurrent and repeated calls share one ack.
    /// If the ack fails, the call throws and the next call tries again; for example, it throws
    /// <see cref="InvalidOperationException"/> when the channel the response was received on is closed.
    /// </remarks>
    /// <param name="cancellationToken">
    /// Cancels waiting for the ack in this call only: the ack itself is still sent.
    /// </param>
    public Task ConfirmAcknowledgeAsync(CancellationToken cancellationToken = default)
    {
        return GetOrStartConfirmation().WaitAsync(cancellationToken);
    }

    private Task GetOrStartConfirmation()
    {
        while (true)
        {
            var existing = Volatile.Read(ref _confirmationTask);
            if (existing is { IsFaulted: false, IsCanceled: false }) return existing;

            var confirmation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (Interlocked.CompareExchange(ref _confirmationTask, confirmation.Task, existing) != existing) continue;

            _ = ConfirmAsync(confirmation);

            return confirmation.Task;
        }
    }

    private async Task ConfirmAsync(TaskCompletionSource confirmation)
    {
        try
        {
            await _confirmationAction.Invoke();
            confirmation.SetResult();
        }
        catch (Exception e)
        {
            confirmation.SetException(e);
        }
    }
}
