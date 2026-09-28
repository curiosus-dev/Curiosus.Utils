using System.Threading;
using System.Threading.Tasks;
using Curiosus.Tools;

namespace Curiosus.Notifications.EMail
{
    /// <summary>
    /// Class for making some action after EMail notification has been sent.
    /// </summary>
    public interface IEMailNotificationPostProcessor
    {
        /// <summary>
        /// Processes specified EMail notification after notification has been sent.
        /// </summary>
        /// <param name="notification">Sent EMail notification.</param>
        /// <param name="sendingResult">Result of sending <paramref name="notification"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns></returns>
        public ValueTask ProcessAsync(EmailNotification notification, Response sendingResult, CancellationToken cancellationToken = default);
    }
}
