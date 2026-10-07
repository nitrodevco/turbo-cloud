using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The hotel's product data: the name and description the client shows for a catalog offer, by
/// the code its name key (<c>localization_id</c>) gives. Habbo's taken in without losing the
/// hotel's own changes - each field compared three ways, as a furniture field is - and products
/// edited, added and removed by staff. Every change is a change set
/// (<see cref="IGamedataHistoryService"/>).
/// </summary>
public interface IGamedataProductService
{
    /// <summary>The newest version of Habbo's product data found; null before the first check.</summary>
    public Task<HabboProductVersionSnapshot?> GetLatestVersionAsync(CancellationToken ct);

    /// <summary>What taking in the version would do; the newest when none is named. Null when there is no such version.</summary>
    public Task<ProductImportPreview?> PreviewImportAsync(int? versionId, CancellationToken ct);

    /// <summary>
    /// Takes the version in. Null when there is no such version, or when the hotel already had
    /// everything; otherwise the change set made.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        int versionId,
        PlayerId? player,
        CancellationToken ct
    );

    /// <summary>Products whose code, name or description holds the words, by code: a page of them.</summary>
    public Task<ProductSearchResult> SearchAsync(string? query, int page, CancellationToken ct);

    /// <summary>The products of these codes - what the catalog's offers name - those there are.</summary>
    public Task<ImmutableArray<ProductEntrySnapshot>> LookupAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken ct
    );

    /// <summary>
    /// Sets a product, adding it when there is none. Throws <see cref="System.ArgumentException"/>
    /// for an empty code or one too long.
    /// </summary>
    public Task<ProductEntrySnapshot> SaveAsync(
        string code,
        string? name,
        string? description,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Removes a product; false when there is none. An import leaves one of Habbo's removed,
    /// unless Habbo changes it.
    /// </summary>
    public Task<bool> DeleteAsync(string code, PlayerId player, CancellationToken ct);
}
