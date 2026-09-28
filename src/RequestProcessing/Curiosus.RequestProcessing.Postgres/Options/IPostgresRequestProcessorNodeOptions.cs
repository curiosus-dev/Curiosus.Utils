using Curiosus.Configuration;

namespace Curiosus.RequestProcessing.Postgres
{
    public interface IPostgresRequestProcessorNodeOptions: ILoggableOptions, IValidatableOptions
    {
        PostgresEventReceiverOptions PostgresEventReceiver { get; }
    }
}
