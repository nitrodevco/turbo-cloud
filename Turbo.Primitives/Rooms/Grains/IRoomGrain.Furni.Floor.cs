using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> PlaceFloorItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot item,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    );
    public Task<bool> MoveFloorItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Rotation rot,
        CancellationToken ct
    );
    public Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        UpdateWiredMessage update,
        CancellationToken ct
    );
    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    );
    public Task<ImmutableArray<RoomFloorItemSnapshot>> GetAllFloorItemSnapshotsAsync(
        CancellationToken ct
    );

    /// <summary>
    /// A wired box's editor data, or null when the item is not wired or the caller may not read
    /// the room's wired.
    /// </summary>
    public Task<WiredDataSnapshot?> GetWiredDataSnapshotByFloorItemIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    );

    /// <summary>
    /// All variables in the room, or null when the caller may not read the room's wired.
    /// </summary>
    public Task<WiredVariablesSnapshot?> GetWiredVariablesSnapshotAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>
    /// Variable values bound to one target, or null when the caller may not read the room's
    /// wired.
    /// </summary>
    public Task<List<(
        WiredVariableId id,
        WiredVariableValue value
    )>?> GetAllVariablesForBindingAsync(
        ActionContext ctx,
        WiredVariableBinding binding,
        CancellationToken ct
    );
}
