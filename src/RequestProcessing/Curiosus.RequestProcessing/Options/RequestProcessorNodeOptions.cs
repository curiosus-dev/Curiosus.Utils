using System.ComponentModel.DataAnnotations;

namespace Curiosus.RequestProcessing
{
    /// <summary>
    /// Настройки приложения-обработчика запросов из очереди.
    /// </summary>
    /// <remarks>
    /// Bound from the <see cref="SectionName"/> section and validated with data annotations when the host starts (see
    /// <see cref="RequestProcessorIocExtensions.AddRequestProcessor{TRequest,TWorker,TWorkerParams,TProcessorBootstrapper,TOptions,TDispatcher,TProcessingRequestInfo}"/>).
    /// Derive from it to add options of a transport or of the app; mark nested options with
    /// <see cref="Microsoft.Extensions.Options.ValidateObjectMembersAttribute"/> to validate them too.
    /// </remarks>
    public class RequestProcessorNodeOptions
    {
        /// <summary>
        /// Default configuration section of the options.
        /// </summary>
        public const string SectionName = "RequestProcessor";

        /// <summary>
        /// Уникальное имя обработчика запросов из очереди.
        /// </summary>
        /// <remarks>
        /// Используется для различия нод, которые взяли запрос в обработку при параллельной работе
        /// (каждая нода, когда берет запрос, лочит его, указывая свое имя).
        /// Например, ISACR-1, ISACR-WCIOM.
        /// </remarks>
        [Required]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Время периодической проверки базы на запросы, события от которых потерялись/не дошли, секунды. At least 1.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int EventsPeriodicCheckSec { get; set; } = 60;

        /// <summary>
        /// Количество воркеров. At least 1.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int WorkersCount { get; set; }

        /// <summary>
        /// Период для записи в лог состояния диспетчера и воркеров в лог, секунды. At least 1.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int StateFlushPeriodSec { get; set; } = 60;
    }
}
