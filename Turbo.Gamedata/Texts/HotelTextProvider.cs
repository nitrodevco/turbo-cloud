using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Texts;

namespace Turbo.Gamedata.Texts;

/// <summary>
/// The hotel's texts from <c>gamedata_texts</c> (<see cref="IHotelTextProvider"/>), read by the
/// keys asked for and never as a whole list. What was read is kept a while
/// (<see cref="GamedataConfig.TextCacheSeconds"/>, at most <see cref="GamedataConfig.TextCacheSize"/>
/// keys), a key the hotel has no text for included, so a reply sent again and again asks once; an
/// edit or import forgets it all (<see cref="Invalidate"/>).
/// <para>
/// A text may be nothing but another's key (<c>wiredfurni.params.action.dance.1</c> is
/// <c>${widget.memenu.dance1}</c>): it is followed to the end, up to
/// <see cref="GamedataConfig.TextKeyDepth"/> steps. A text that merely contains a reference is the
/// client's to expand.
/// </para>
/// </summary>
internal sealed class HotelTextProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<GamedataConfig> config,
    TimeProvider time
) : IHotelTextProvider
{
    private const string KEY_OPEN = "${";
    private const char KEY_CLOSE = '}';

    // Keys per query: an IN list this long is still one round trip.
    private const int QUERY_BATCH = 500;

    private readonly GamedataConfig _config = config.Value;
    private readonly ConcurrentDictionary<string, Cached> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CachedFamily> _families = new(
        StringComparer.Ordinal
    );

    public async Task<HotelTexts> GetTextsAsync(IEnumerable<string> keys, CancellationToken ct)
    {
        var wanted = keys.Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var raw = await ReadAsync(wanted, ct).ConfigureAwait(false);

        return new HotelTexts(await ResolveAsync(raw, ct).ConfigureAwait(false));
    }

    public async Task<HotelTexts> GetTextsByPrefixAsync(string prefix, CancellationToken ct)
    {
        var now = time.GetUtcNow();

        if (_families.TryGetValue(prefix, out var cached) && cached.Until > now)
            return cached.Texts;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .GamedataTexts.AsNoTracking()
            .Where(x => x.Key.StartsWith(prefix))
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var raw = new Dictionary<string, string?>(StringComparer.Ordinal);

        // The database compares as its column does; only an exact prefix belongs to the family.
        foreach (var row in rows.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)))
            raw[row.Key] = row.Value;

        var texts = new HotelTexts(await ResolveAsync(raw, ct).ConfigureAwait(false));

        _families[prefix] = new CachedFamily(
            texts,
            now.AddSeconds(Math.Max(0, _config.TextCacheSeconds))
        );

        return texts;
    }

    /// <summary>These stored values, each one that is only another's key followed to its text.</summary>
    private async Task<Dictionary<string, string>> ResolveAsync(
        Dictionary<string, string?> raw,
        CancellationToken ct
    )
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);

        // Follow each text that is only a reference, reading the keys it leads to a step at a time.
        var pending = raw.Where(x => x.Value is not null)
            .ToDictionary(x => x.Key, x => x.Value!, StringComparer.Ordinal);

        for (var depth = 0; depth < _config.TextKeyDepth && pending.Count > 0; depth++)
        {
            var references = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (key, value) in pending)
            {
                if (ReferenceOf(value) is { } next)
                    references[key] = next;
                else
                    resolved[key] = value;
            }

            if (references.Count == 0)
            {
                pending.Clear();

                break;
            }

            var targets = await ReadAsync(references.Values.Distinct(), ct).ConfigureAwait(false);

            pending = [];

            foreach (var (key, next) in references)
            {
                if (targets.TryGetValue(next, out var target) && target is not null)
                    pending[key] = target;
                else
                    resolved[key] = raw[key]!; // points nowhere: left as written
            }
        }

        // A chain still a reference after the last step is a loop: left as it was written.
        foreach (var key in pending.Keys)
            resolved[key] = raw[key]!;

        return resolved;
    }

    public async Task<string?> GetTextAsync(string key, CancellationToken ct) =>
        (await GetTextsAsync([key], ct).ConfigureAwait(false)).TryGetText(key, out var text)
            ? text
            : null;

    public void Invalidate()
    {
        _cache.Clear();
        _families.Clear();
    }

    /// <summary>The values of these keys as stored, from the cache or the database; null for a key the hotel has no text for.</summary>
    private async Task<Dictionary<string, string?>> ReadAsync(
        IEnumerable<string> keys,
        CancellationToken ct
    )
    {
        var now = time.GetUtcNow();
        var found = new Dictionary<string, string?>(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach (var key in keys)
        {
            if (_cache.TryGetValue(key, out var cached) && cached.Until > now)
                found[key] = cached.Value;
            else
                missing.Add(key);
        }

        if (missing.Count == 0)
            return found;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        foreach (var batch in missing.Chunk(QUERY_BATCH))
        {
            var rows = await dbCtx
                .GamedataTexts.AsNoTracking()
                .Where(x => batch.Contains(x.Key))
                .Select(x => new { x.Key, x.Value })
                .ToListAsync(ct)
                .ConfigureAwait(false);
            var values = rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

            foreach (var key in batch)
                found[key] = values.GetValueOrDefault(key);
        }

        Remember(missing, found, now);

        return found;
    }

    private void Remember(List<string> keys, Dictionary<string, string?> values, DateTimeOffset now)
    {
        // Bounded by forgetting everything when full: simple, and what was asked often comes back.
        if (_cache.Count + keys.Count > Math.Max(1, _config.TextCacheSize))
            _cache.Clear();

        var until = now.AddSeconds(Math.Max(0, _config.TextCacheSeconds));

        foreach (var key in keys)
            _cache[key] = new Cached(values[key], until);
    }

    /// <summary>The key a text is nothing but a reference to; null for any other text.</summary>
    private static string? ReferenceOf(string value) =>
        value.StartsWith(KEY_OPEN, StringComparison.Ordinal)
        && value.IndexOf(KEY_CLOSE, StringComparison.Ordinal) == value.Length - 1
        && value.Length > KEY_OPEN.Length + 1
            ? value[KEY_OPEN.Length..^1]
            : null;

    private readonly record struct Cached(string? Value, DateTimeOffset Until);

    private readonly record struct CachedFamily(HotelTexts Texts, DateTimeOffset Until);
}
