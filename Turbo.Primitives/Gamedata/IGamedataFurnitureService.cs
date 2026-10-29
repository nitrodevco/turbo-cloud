using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The hotel's furniture definitions as the source of its furnidata: Habbo's updates taken in
/// without losing the hotel's own changes, and definitions edited by staff. Every change is
/// recorded as a change set (<see cref="IGamedataHistoryService"/>).
/// <para>
/// An import compares each field three ways: Habbo's new value, Habbo's value when it was last
/// taken in, and the definition's. A field still at Habbo's last value takes the new one; a field
/// the hotel changed keeps the hotel's value.
/// </para>
/// </summary>
public interface IGamedataFurnitureService
{
    /// <summary>Previews missing known vending defaults, preserving saved usage and custom data.</summary>
    public Task<VendingRepairPreviewSnapshot> PreviewVendingRepairAsync(
        int page,
        CancellationToken ct
    );

    /// <summary>
    /// Applies at most 100 explicitly selected current previews as one edit/history transaction.
    /// Throws <see cref="VendingRepairConflictException"/> if any selected definition changed;
    /// throws <see cref="System.ArgumentException"/> for an invalid selection. Nothing is written
    /// when any selected preview is stale. Usage changes require explicit per-item acceptance.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> RepairVendingAsync(
        IReadOnlyCollection<VendingRepairSelectionSnapshot> items,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>What importing the release would do; the newest release when none is named. Null when there is no such release.</summary>
    public Task<FurnitureImportPreview?> PreviewImportAsync(int? releaseId, CancellationToken ct);

    /// <summary>
    /// Takes in the release's furniture. Null when there is no such release; otherwise the
    /// change set made, which is null too when the hotel already had everything.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> ImportAsync(
        int releaseId,
        PlayerId? player,
        CancellationToken ct
    );

    /// <summary>
    /// A page (from 0) of the floor and wall definitions, by classname: those whose classname or
    /// name holds <paramref name="query"/> (or whose id or sprite id it is), of
    /// <paramref name="type"/> when given, carrying <paramref name="tag"/> when given.
    /// </summary>
    public Task<FurnitureListPage> ListAsync(
        string? query,
        ProductType? type,
        FurnitureTag? tag,
        int page,
        CancellationToken ct
    );

    public Task<FurnitureDefinitionDetail?> GetDefinitionAsync(int id, CancellationToken ct);

    /// <summary>
    /// Changes the definition's furnidata fields, given by their furnidata keys (<c>name</c>,
    /// <c>xdim</c>, <c>canstandon</c>, ...), recorded as an edit when anything differed. Throws
    /// <see cref="System.ArgumentException"/> for a key that can't be edited or a value of the
    /// wrong kind. Returns the definition as it now is; null when there is no such definition.
    /// </summary>
    public Task<FurnitureDefinitionDetail?> UpdateDefinitionAsync(
        int id,
        JsonObject fields,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// The definition a furniture bundle's own data says it would be (<see cref="FurnitureDraft"/>).
    /// Null when there is no furniture bundle of that name with a file. Throws
    /// <see cref="System.ArgumentException"/> when its file can't be read.
    /// </summary>
    public Task<FurnitureDraft?> DraftFromBundleAsync(string bundle, CancellationToken ct);

    /// <summary>
    /// Makes a definition of <paramref name="type"/> with these fields (furnidata and server keys,
    /// as <see cref="UpdateDefinitionAsync"/> takes them; <c>classname</c> is needed), and one more
    /// for each of <paramref name="colors"/> named <c>classname*N</c>: one change set, which rolls
    /// back as a whole. Without an <c>id</c> each gets the sprite id above every one the hotel and
    /// Habbo use; without a <c>logic</c>, the one a new item of its classname gets. Throws
    /// <see cref="System.ArgumentException"/> as an edit does, and for a classname or sprite id
    /// another definition of the kind has. Returns the ids made, the plain one first.
    /// </summary>
    public Task<ImmutableArray<int>> CreateDefinitionsAsync(
        ProductType type,
        JsonObject fields,
        IReadOnlyCollection<int> colors,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// How many definitions hold a value of their own in each of these fields (furnidata keys),
    /// where Habbo's newest release has another, taken in or not. Only furniture Habbo has is
    /// counted.
    /// Throws <see cref="System.ArgumentException"/> for a key a definition doesn't hold.
    /// </summary>
    public Task<HabboValuesPreview> PreviewHabboValuesAsync(
        IReadOnlyCollection<string> fields,
        CancellationToken ct
    );

    /// <summary>
    /// Puts Habbo's values back in these fields, over the hotel's, for every definition of
    /// furniture Habbo has: one change set, which rolls back as a whole. Null when every value
    /// already was Habbo's. Throws <see cref="System.ArgumentException"/> as the preview does.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> TakeHabboValuesAsync(
        IReadOnlyCollection<string> fields,
        PlayerId player,
        CancellationToken ct
    );

    /// <summary>
    /// Gives definitions a logic: <paramref name="logic"/> to each of <paramref name="ids"/>, or,
    /// without a logic, each its matching one (<see cref="Snapshots.FurnitureListItem.MatchingLogic"/>);
    /// without ids, every definition tagged <see cref="Enums.FurnitureTag.LogicMismatch"/> its
    /// matching one. One change set, which rolls back as a whole; null when nothing changed.
    /// Throws <see cref="System.ArgumentException"/> for a logic the server does not have, or a
    /// logic without ids.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> SetLogicAsync(
        IReadOnlyCollection<int>? ids,
        string? logic,
        PlayerId player,
        CancellationToken ct
    );
}
