using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Gamedata;

/// <summary>
/// An import's preview, kept until what it is made from changes. Working one out reads whole
/// tables and Habbo's file, tens to hundreds of MB for a full hotel, and the panel asks for every
/// kind at once on each visit to its updates page: it is worked out again only for another
/// version, and one at a time, so pages asking together work it out once.
/// </summary>
internal sealed class GamedataPreviewCache<TVersion, TPreview>
    where TVersion : IEquatable<TVersion>
    where TPreview : class
{
    private readonly SemaphoreSlim _working = new(1, 1);
    private Kept? _kept;

    /// <summary>The preview of <paramref name="asked"/> (null: the newest) as of <paramref name="version"/>.</summary>
    public async Task<TPreview?> GetAsync(
        TVersion version,
        int? asked,
        Func<Task<TPreview?>> workOut,
        CancellationToken ct
    )
    {
        if (Find(version, asked) is { } kept)
            return kept;

        await _working.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (Find(version, asked) is { } done)
                return done;

            var preview = await workOut().ConfigureAwait(false);

            _kept = preview is null ? null : new Kept(version, asked, preview);

            return preview;
        }
        finally
        {
            _working.Release();
        }
    }

    private TPreview? Find(TVersion version, int? asked) =>
        _kept is { } kept && kept.Version.Equals(version) && kept.Asked == asked
            ? kept.Preview
            : null;

    private sealed record Kept(TVersion Version, int? Asked, TPreview Preview);
}
