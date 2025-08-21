using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Turbo.Networking.Dispatcher;

public class TokenBucket<TKey>
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly double _refillPerMs;
    private readonly long _cleanupThresholdMs;

    private readonly Stripe[] _stripes;
    private readonly int _mask; // stripes is power-of-two for fast hashing
    private long _lastCleanupTicks;

    private sealed class Stripe
    {
        // Small map guarded by a single lock per stripe.
        public readonly object Gate = new();
        public readonly Dictionary<TKey, State> Map = new(capacity: 128);
    }

    private struct State
    {
        public int Tokens;
        public long LastTicks;
    }

    public TokenBucket(
        int capacity,
        double refillPerSec,
        int stripes = 32,
        TimeSpan? cleanupThreshold = null
    )
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        if (refillPerSec < 0)
            throw new ArgumentOutOfRangeException(nameof(refillPerSec));

        _capacity = capacity;
        _refillPerMs = refillPerSec / 1000.0;
        _cleanupThresholdMs = (long)(
            cleanupThreshold?.TotalMilliseconds ?? TimeSpan.FromMinutes(5).TotalMilliseconds
        );
        _lastCleanupTicks = Environment.TickCount64;

        // round stripes up to next power of two
        var n = 1;
        while (n < Math.Max(1, stripes))
            n <<= 1;
        _stripes = new Stripe[n];
        for (int i = 0; i < n; i++)
            _stripes[i] = new Stripe();
        _mask = n - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int StripeIndex(TKey key)
    {
        // Spread bits & ensure non-negative
        var h = key.GetHashCode();
        unchecked
        {
            h ^= (h << 13);
            h ^= (h >> 17);
            h ^= (h << 5);
        }
        return h & _mask;
    }

    /// <summary>
    /// /// Try to consume 'amount' tokens for 'key'.
    /// Returns true if granted; false if not enough tokens.
    /// </summary>
    public bool TryTake(TKey key, int amount = 1)
    {
        if (amount <= 0)
            return true;

        var now = Environment.TickCount64;
        var s = _stripes[StripeIndex(key)];

        // Periodic cleanup check (only check every ~30 seconds to avoid overhead)
        if (now - _lastCleanupTicks > 30_000)
        {
            TryPerformCleanup(now);
        }

        lock (s.Gate)
        {
            ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(
                s.Map,
                key,
                out bool exists
            );
            if (!exists)
            {
                state.Tokens = _capacity;
                state.LastTicks = now;
            }

            // Refill based on elapsed time
            var elapsedMs = Math.Max(0L, now - state.LastTicks);
            if (elapsedMs > 0)
            {
                var refill = (int)Math.Floor(elapsedMs * _refillPerMs);
                if (refill > 0)
                {
                    var tokens = state.Tokens + refill;
                    state.Tokens = tokens >= _capacity ? _capacity : tokens;
                    state.LastTicks = now;
                }
            }

            if (state.Tokens < amount)
                return false;

            state.Tokens -= amount;
            return true;
        }
    }

    /// <summary>Reset state for a key (e.g., when session drains to 0 pending or disconnects).</summary>
    public void Reset(TKey key)
    {
        var s = _stripes[StripeIndex(key)];
        lock (s.Gate)
        {
            s.Map.Remove(key);
        }
    }

    /// <summary>
    /// Manually trigger cleanup of stale entries across all stripes.
    /// This removes entries that haven't been accessed within the cleanup threshold.
    /// </summary>
    public int CleanupStaleEntries()
    {
        var now = Environment.TickCount64;
        var totalRemoved = 0;

        for (int i = 0; i < _stripes.Length; i++)
        {
            totalRemoved += CleanupStripe(_stripes[i], now);
        }

        _lastCleanupTicks = now;
        return totalRemoved;
    }

    /// <summary>
    /// Try to perform cleanup if enough time has passed since the last cleanup.
    /// This is called automatically during TryTake operations.
    /// </summary>
    private void TryPerformCleanup(long now)
    {
        // Use interlocked compare exchange to ensure only one thread performs cleanup
        var lastCleanup = _lastCleanupTicks;
        if (now - lastCleanup > 30_000) // 30 seconds
        {
            if (Interlocked.CompareExchange(ref _lastCleanupTicks, now, lastCleanup) == lastCleanup)
            {
                // We won the race, perform cleanup on a subset of stripes to spread the work
                var stripesToClean = Math.Max(1, _stripes.Length / 4); // Clean 25% of stripes each time
                var startIndex = (int)(now / 30_000) % _stripes.Length; // Rotate starting point

                for (int i = 0; i < stripesToClean; i++)
                {
                    var stripeIndex = (startIndex + i) % _stripes.Length;
                    CleanupStripe(_stripes[stripeIndex], now);
                }
            }
        }
    }

    /// <summary>
    /// Clean up a single stripe by removing stale entries.
    /// </summary>
    private int CleanupStripe(Stripe stripe, long now)
    {
        var keysToRemove = new List<TKey>();

        lock (stripe.Gate)
        {
            // First pass: identify stale entries
            foreach (var kvp in stripe.Map)
            {
                if (now - kvp.Value.LastTicks > _cleanupThresholdMs)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            // Second pass: remove stale entries
            foreach (var key in keysToRemove)
            {
                stripe.Map.Remove(key);
            }
        }

        return keysToRemove.Count;
    }
}
