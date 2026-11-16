using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Curiosus.RequestProcessing.Postgres
{
    /// <summary>
    /// Options of <see cref="DbEventReceiver"/>.
    /// </summary>
    public class PostgresEventReceiverOptions
    {
        /// <summary>
        /// Время, через которое в случае простоя будет отправлено keep alive сообщение в БД (в сек). 0 turns it off.
        /// </summary>
        [Range(0, int.MaxValue)]
        public int KeepAliveSec { get; set; } = 0;

        /// <summary>
        /// Время ожидания перед переподключением к БД в случае разрыва соединения, мс.
        /// </summary>
        [Range(0, int.MaxValue)]
        public int ReconnectionPauseMs { get; set; } = 100;

        /// <summary>
        /// События, на которые надо подписаться, чтобы узнавать о новых запросах.
        /// </summary>
        /// <remarks>
        /// Names are used exactly as written (case-sensitive): <c>MyChannel</c> receives <c>pg_notify('MyChannel', ...)</c>
        /// and <c>NOTIFY "MyChannel"</c>, but not an unquoted <c>NOTIFY MyChannel</c>, which PostgreSQL folds to lower case.
        /// At least one name; each name is non-empty, at most 63 bytes in UTF-8 and has no NUL character.
        /// </remarks>
        [PgChannelNames]
        public IReadOnlyList<string> EventNames { get; set; } = Array.Empty<string>();
    }
}
