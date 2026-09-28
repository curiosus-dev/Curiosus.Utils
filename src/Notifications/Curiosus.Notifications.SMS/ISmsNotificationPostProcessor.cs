using System.Threading;
using System.Threading.Tasks;
using Curiosus.SMS;
using Curiosus.Tools;

namespace Curiosus.Notifications.SMS
{
    /// <summary>
    /// Class for making some action after SMS notification has been sent.
    /// </summary>
    public interface ISmsNotificationPostProcessor
    {
        /// <summary>
        /// Processes specified SMS notification after notification has been sent.
        /// </summary>
        /// <param name="notification">Sent EMail notification.</param>
        /// <param name="result">Result of sending <paramref name="notification"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        public ValueTask ProcessAsync(SmsNotification notification, Response<SmsSentResult> result, CancellationToken cancellationToken = default);
    }
}
