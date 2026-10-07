using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
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
}
