using System.Collections.Generic;
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

    /// <summary>The hotel's values of the keys it has.</summary>
    public Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken ct
    );

    /// <summary>
    /// Adds the texts the hotel has none of, leaving every one it has as it is: a plugin's own, as
    /// it loads. A text the file can't hold is left out. One change set by nobody, with
    /// <paramref name="summary"/>; null when the hotel had them all.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> SeedAsync(
        IReadOnlyDictionary<string, string> values,
        string summary,
        CancellationToken ct
    );

    /// <summary>
    /// Removes the texts that still hold the value given, leaving any the hotel changed: a plugin's
    /// own, as it is uninstalled. One change set; null when none was left as given.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> RemoveUnchangedAsync(
        IReadOnlyDictionary<string, string> values,
        string summary,
        PlayerId player,
        CancellationToken ct
    );
}
