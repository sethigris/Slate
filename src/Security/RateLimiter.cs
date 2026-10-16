using System;
using System.Collections.Generic;

namespace Slate.Security
{
    public sealed class RateLimiter
    {
        private sealed class Bucket
        {
            public long WindowNumber;
            public int Count;
            public DateTime LastSeenUtc;
        }

        private readonly object _sync = new object();
        private readonly Dictionary<string, Bucket> _buckets;
        private readonly int _limitPerMinute;
        private int _callsSinceCleanup;

        public RateLimiter(int limitPerMinute)
        {
            if (limitPerMinute < 1)
            {
                throw new ArgumentOutOfRangeException("limitPerMinute");
            }

            _limitPerMinute = limitPerMinute;
            _buckets = new Dictionary<string, Bucket>(StringComparer.OrdinalIgnoreCase);
        }

        public bool IsAllowed(string clientKey)
        {
            return IsAllowed(clientKey, DateTime.UtcNow);
        }

        public bool IsAllowed(string clientKey, DateTime nowUtc)
        {
            if (String.IsNullOrWhiteSpace(clientKey))
            {
                clientKey = "unknown";
            }

            long window = nowUtc.Ticks / TimeSpan.TicksPerMinute;
            lock (_sync)
            {
                Bucket bucket;
                if (!_buckets.TryGetValue(clientKey, out bucket))
                {
                    bucket = new Bucket();
                    bucket.WindowNumber = window;
                    bucket.Count = 0;
                    bucket.LastSeenUtc = nowUtc;
                    _buckets.Add(clientKey, bucket);
                }

                if (bucket.WindowNumber != window)
                {
                    bucket.WindowNumber = window;
                    bucket.Count = 0;
                }

                bucket.LastSeenUtc = nowUtc;
                bucket.Count++;
                _callsSinceCleanup++;

                if (_callsSinceCleanup >= 256)
                {
                    Cleanup(nowUtc);
                    _callsSinceCleanup = 0;
                }

                return bucket.Count <= _limitPerMinute;
            }
        }

        public int GetRetryAfterSeconds(DateTime nowUtc)
        {
            long ticksIntoMinute = nowUtc.Ticks % TimeSpan.TicksPerMinute;
            int seconds = (int)Math.Ceiling(
                (TimeSpan.TicksPerMinute - ticksIntoMinute) / (double)TimeSpan.TicksPerSecond);
            return Math.Max(1, seconds);
        }

        private void Cleanup(DateTime nowUtc)
        {
            if (_buckets.Count == 0)
            {
                return;
            }

            List<string> stale = new List<string>();
            DateTime cutoff = nowUtc.AddMinutes(-10);
            foreach (KeyValuePair<string, Bucket> entry in _buckets)
            {
                if (entry.Value.LastSeenUtc < cutoff)
                {
                    stale.Add(entry.Key);
                }
            }

            int index;
            for (index = 0; index < stale.Count; index++)
            {
                _buckets.Remove(stale[index]);
            }
        }
    }
}
