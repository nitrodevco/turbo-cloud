using System;
using System.Collections.Concurrent;

namespace Turbo.Networking.Dispatcher;

public class TokenBucket<TKey>(int capacity, int refillPerSec) where TKey : class
{
    private readonly int _capacity = capacity;
    private readonly int _refillPerSec = refillPerSec;
    private readonly ConcurrentDictionary<TKey, State> _s = new();

    private readonly record struct State(int Tokens, long LastTicks);

    public bool TryTake(TKey key, int amount = 1)
    {
        while (true)
        {
            var now = Environment.TickCount64;
            var cur = _s.GetOrAdd(key, _ => new State(_capacity, now));

            // refill based on elapsed time
            var elapsedMs = Math.Max(0, now - cur.LastTicks);
            var refill = (int)(elapsedMs * _refillPerSec / 1000.0);
            var tokens = Math.Min(_capacity, cur.Tokens + refill);

            if (tokens < amount)
            {
                // not enough; update last ticks to now so refill starts
                _s[key] = new State(tokens, now);
                return false;
            }

            var next = new State(tokens - amount, now);
            if (_s.TryUpdate(key, next, cur)) return true;
            // contention? loop
        }
    }

    public void Reset(TKey key) => _s.TryRemove(key, out _);
}