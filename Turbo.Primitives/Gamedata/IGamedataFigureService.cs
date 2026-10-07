using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The hotel's figure data: the colours, kinds and pieces of clothing the client draws avatars
/// from, and the rules a figure is checked against (who may wear what). Habbo's taken in without
/// losing the hotel's own changes - each field compared three ways, as a furniture field is - and
/// records edited, added and removed by staff. Every change is a change set
/// (<see cref="IGamedataHistoryService"/>).
/// </summary>
public interface IGamedataFigureService
{
    /// <summary>The newest version of Habbo's figure data found; null before the first check.</summary>
    public Task<HabboFigureVersionSnapshot?> GetLatestVersionAsync(CancellationToken ct);

    /// <summary>What taking in the version would do; the newest when none is named. Null when there is no such version.</summary>
    public Task<FigureImportPreview?> PreviewImportAsync(int? versionId, CancellationToken ct);

    /// <summary>
    /// Takes the version in. Null when there is no such version, or when the hotel already had
    /// everything; otherwise the change set made.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        int versionId,
        PlayerId? player,
        CancellationToken ct
    );

    /// <summary>
    /// Records of a kind, those under the group when one is named (a kind of clothing's pieces, a
    /// palette's colours), whose key or fields hold the words, and that have every field given in
    /// <paramref name="has"/>: a page of them, by key. Each of <paramref name="has"/> is a field as
    /// the record's JSON writes it (<c>"gender":"M"</c>), or several split by <c>|</c>, any of which
    /// will do (<c>"club":1|"club":2</c>).
    /// </summary>
    public Task<FigureSearchResult> SearchAsync(
        FigureRecordKind kind,
        string? group,
        string? query,
        IReadOnlyList<string> has,
        int page,
        CancellationToken ct
    );

    /// <summary>The kinds of clothing with how many pieces each has, and the id a new piece takes.</summary>
    public Task<FigureKindsSnapshot> GetKindsAsync(CancellationToken ct);

    /// <summary>
    /// Sets a record from its fields as JSON, adding it when there is none; its key is read from
    /// them. Throws <see cref="System.ArgumentException"/> for fields that don't make a record.
    /// </summary>
    public Task<FigureEntrySnapshot> SaveAsync(
        FigureRecordKind kind,
        string data,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>Every palette with its colours by order, and the kinds of clothing coloured from each.</summary>
    public Task<ImmutableArray<FigurePaletteSnapshot>> GetPalettesAsync(CancellationToken ct);

    /// <summary>
    /// Sets these records and removes those keys, all of one kind, as one change set described by
    /// the summary: a palette reordered, recoloured and pruned at once. A record as it already is
    /// changes nothing; the number changed. Throws <see cref="System.ArgumentException"/> before
    /// anything is written when any record doesn't make one.
    /// </summary>
    public Task<int> SaveBatchAsync(
        FigureRecordKind kind,
        IReadOnlyList<string> save,
        IReadOnlyList<string> delete,
        string summary,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Removes a record; false when there is none. An import leaves one of Habbo's removed, unless
    /// Habbo changes it.
    /// </summary>
    public Task<bool> DeleteAsync(
        FigureRecordKind kind,
        string key,
        PlayerId player,
        CancellationToken ct
    );
}
