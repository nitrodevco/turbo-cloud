using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The hotel's external texts: Habbo's taken in without losing the hotel's own changes, and texts
/// edited, added and removed by staff. Each key is compared three ways on an import, as a
/// furniture field is: Habbo's new value, Habbo's value when last taken in, and the hotel's. Every
/// change is a change set (<see cref="IGamedataHistoryService"/>).
/// </summary>
public interface IGamedataTextService
{
    /// <summary>The newest version of Habbo's texts found; null before the first check.</summary>
    public Task<HabboTextVersionSnapshot?> GetLatestVersionAsync(CancellationToken ct);

    /// <summary>What taking in the version would do; the newest when none is named. Null when there is no such version.</summary>
    public Task<TextImportPreview?> PreviewImportAsync(int? versionId, CancellationToken ct);

    /// <summary>
    /// Takes the version in. Null when there is no such version, or when the hotel already had
    /// everything; otherwise the change set made.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        int versionId,
        PlayerId? player,
        CancellationToken ct
    );

    /// <summary>Texts whose key or value holds the words, by key: a page of them.</summary>
    public Task<TextSearchResult> SearchAsync(string? query, int page, CancellationToken ct);

    /// <summary>
    /// Sets a text, adding it when there is none. Throws <see cref="System.ArgumentException"/> for
    /// what the file can't hold: an empty key, a key with <c>=</c> or a line break or starting with
    /// <c>#</c>, a value with a line break (it is written <c>\n</c>).
    /// </summary>
    public Task<TextEntrySnapshot> SaveAsync(
        string key,
        string value,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Removes a text; false when there is none. An import leaves one of Habbo's removed, unless
    /// Habbo changes it.
    /// </summary>
    public Task<bool> DeleteAsync(string key, PlayerId player, CancellationToken ct);
}
