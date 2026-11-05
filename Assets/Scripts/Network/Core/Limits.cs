// Naija Streets (Unity) — reconnect backoff, rate limits, chat rules,
// street-card cache. Spec: docs/specs/netcode-m5.md §3.3-3.5, §4.4.
// UNVERIFIED (2026-11-05): not compiled yet.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NaijaLife.Network
{
    /// <summary>Full-jitter exponential backoff: random(0, min(30 s, 1 s * 2^attempt)).</summary>
    public sealed class Backoff
    {
        readonly Random _rng;
        public int Attempt { get; private set; }
        public Backoff(int seed = 0) { _rng = seed == 0 ? new Random() : new Random(seed); }

        public static double Cap(int attempt) =>
            Math.Min(NetLimits.BackoffMaxSeconds, NetLimits.BackoffBaseSeconds * Math.Pow(2, Math.Min(attempt, 16)));

        public static double Delay(int attempt, double random01) => Cap(attempt) * random01;

        /// <summary>Seconds to wait before the next try; counts the attempt.</summary>
        public double Next() => Delay(Attempt++, _rng.NextDouble());

        public void Reset() => Attempt = 0;
    }

    /// <summary>At most N per sliding second.</summary>
    public sealed class RateLimiter
    {
        readonly int _perSecond;
        readonly Queue<double> _times = new Queue<double>();
        public RateLimiter(int perSecond) { _perSecond = perSecond; }

        public bool TryAcquire(double now)
        {
            while (_times.Count > 0 && now - _times.Peek() >= 1.0) _times.Dequeue();
            if (_times.Count >= _perSecond) return false;
            _times.Enqueue(now);
            return true;
        }
    }

    /// <summary>Same per-sender limit on receive, so a modified client cannot spam others.</summary>
    public sealed class PerSenderLimiter
    {
        readonly int _perSecond;
        readonly Dictionary<NetPlayerId, RateLimiter> _by = new Dictionary<NetPlayerId, RateLimiter>();
        public PerSenderLimiter(int perSecond) { _perSecond = perSecond; }

        public bool TryAcquire(NetPlayerId from, double now)
        {
            if (!_by.TryGetValue(from, out RateLimiter r)) { r = new RateLimiter(_perSecond); _by[from] = r; }
            return r.TryAcquire(now);
        }

        public void Forget(NetPlayerId id) => _by.Remove(id);
    }

    public static class ChatRules
    {
        /// <summary>Trim, drop control characters, collapse runs of spaces.</summary>
        public static string Normalize(string text)
        {
            if (text == null) return string.Empty;
            var sb = new StringBuilder(text.Length);
            bool space = false;
            foreach (char ch in text.Trim())
            {
                if (char.IsControl(ch)) continue;
                if (char.IsWhiteSpace(ch)) { if (!space) sb.Append(' '); space = true; continue; }
                space = false;
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>Check a normalized line. Blocklist words are lowercase; matched on letters only.</summary>
        public static ChatRefusal Check(string normalized, ICollection<string> blocklist)
        {
            if (string.IsNullOrEmpty(normalized)) return ChatRefusal.Empty;
            if (normalized.Length > NetLimits.ChatMaxChars) return ChatRefusal.TooLong;
            if (blocklist != null && blocklist.Count > 0)
            {
                string letters = LettersOnly(normalized);
                foreach (string w in blocklist)
                    if (!string.IsNullOrEmpty(w) && letters.Contains(w)) return ChatRefusal.Blocked;
            }
            return ChatRefusal.None;
        }

        /// <summary>
        /// Safe for TextMeshPro with rich text on. Chat text fields should also have
        /// richText off; this is the second guard.
        /// </summary>
        public static string EscapeForRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return "<noparse>" + text.Replace("</noparse>", "</no parse>") + "</noparse>";
        }

        static string LettersOnly(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s) if (char.IsLetter(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
    }

    /// <summary>
    /// IStreetCardService over a fetch function (the Supabase nl_street_card RPC).
    /// Caches each card 10 minutes and shares one in-flight call per player.
    /// </summary>
    public sealed class StreetCardCache : IStreetCardService
    {
        readonly Func<NetPlayerId, CancellationToken, Task<StreetCard>> _fetch;
        readonly Func<double> _clock;
        readonly Dictionary<NetPlayerId, (StreetCard card, double until)> _cache =
            new Dictionary<NetPlayerId, (StreetCard, double)>();
        readonly Dictionary<NetPlayerId, Task<StreetCard>> _inflight = new Dictionary<NetPlayerId, Task<StreetCard>>();

        public StreetCardCache(Func<NetPlayerId, CancellationToken, Task<StreetCard>> fetch, Func<double> clock)
        {
            _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Task<StreetCard> GetAsync(NetPlayerId id, CancellationToken cancel)
        {
            if (!id.IsValid) return Task.FromResult<StreetCard>(null);
            double now = _clock();
            if (_cache.TryGetValue(id, out var hit) && hit.until > now) return Task.FromResult(hit.card);
            if (_inflight.TryGetValue(id, out Task<StreetCard> running)) return running;
            Task<StreetCard> t = FetchAndStore(id, cancel);
            if (!t.IsCompleted) _inflight[id] = t; // a fetch that finished at once must not stick here
            return t;
        }

        async Task<StreetCard> FetchAndStore(NetPlayerId id, CancellationToken cancel)
        {
            try
            {
                StreetCard card = await _fetch(id, cancel);
                if (card != null) card.Id = id; // the id is ours, never the payload's
                // A failed lookup (null) is cached for 1 minute only, so it retries soon.
                double ttl = card != null ? NetLimits.StreetCardCacheSeconds : 60.0;
                _cache[id] = (card, _clock() + ttl);
                return card;
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception) { return null; }
            finally { _inflight.Remove(id); }
        }

        public void Clear() => _cache.Clear();
    }
}
