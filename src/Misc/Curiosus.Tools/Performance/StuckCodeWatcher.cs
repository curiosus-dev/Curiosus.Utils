using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Curiosus.Tools.Performance
{
    /// <summary>
    /// Stuck code detector. Allows you to set the time for a piece of code to be executed.
    /// If block of code doesn't completed for a specified time, the information about it will appear in the log.
    /// </summary>
    internal class StuckCodeWatcher
    {
        private readonly struct StuckCodeItem
        {
            public StuckCodeItem(string label, DateTime enteredUtc, long enteredTimestamp, int timeoutSeconds, int threadId)
            {
                Label = label;
                EnteredUtc = enteredUtc;
                EnteredTimestamp = enteredTimestamp;
                TimeoutSeconds = timeoutSeconds;
                ThreadId = threadId;
            }

            public string Label { get; }
            public DateTime EnteredUtc { get; }
            public long EnteredTimestamp { get; }
            public int TimeoutSeconds { get; }
            public int ThreadId { get; }
        }

        private sealed class StuckCodeItemFinalizer : IDisposable
        {
            private readonly StuckCodeWatcher _watcher;
            private readonly long _key;

            public StuckCodeItemFinalizer(StuckCodeWatcher watcher, long key)
            {
                _watcher = watcher;
                _key = key;
            }

            public void Dispose() => _watcher.Leave(_key);
        }

        private readonly ConcurrentDictionary<long, StuckCodeItem> _items = new();
        private long _lastKey;

        internal int Count => _items.Count;

        public IDisposable Enter(string label, int timeoutSeconds)
        {
            if (String.IsNullOrEmpty(label))
                throw new ArgumentNullException(nameof(label));
            if (timeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));

            var key = Interlocked.Increment(ref _lastKey);
            _items[key] = new StuckCodeItem(
                label,
                DateTime.UtcNow,
                Stopwatch.GetTimestamp(),
                timeoutSeconds,
                Environment.CurrentManagedThreadId);

            return new StuckCodeItemFinalizer(this, key);
        }

        private void Leave(long key)
        {
            _items.TryRemove(key, out _);
        }

        public void Validate(ILogger? logger) => Validate(logger, Stopwatch.GetTimestamp());

        internal void Validate(ILogger? logger, long nowTimestamp)
        {
            foreach (var item in _items.Values)
            {
                var duration = (int)Stopwatch.GetElapsedTime(item.EnteredTimestamp, nowTimestamp).TotalSeconds;
                if (duration >= item.TimeoutSeconds)
                {
                    logger?.LogError("STUCK CODE: \"{StuckCodeLabel}\", duration {StuckCodeDuration} (entered at {StuckCodeEnterTime} from thread #{StuckCodeThreadId})",
                        item.Label,
                        String.Create(CultureInfo.InvariantCulture, $"{duration / 60:d2}:{duration % 60:d2}"),
                        item.EnteredUtc.ToString("O", CultureInfo.InvariantCulture),
                        item.ThreadId);
                }
            }
        }
    }
}
