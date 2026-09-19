using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>Saves a wired box from its editor. False when refused or invalid.</summary>
    public Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        UpdateWiredMessage update,
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

    /// <summary>
    /// The wired settings tab data, or null when the caller may not read the room's wired.
    /// </summary>
    public Task<WiredRoomSettingsSnapshot?> GetWiredRoomSettingsAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>
    /// Updates who may read and modify wired plus the room's wired timezone. Owner only.
    /// </summary>
    public Task<bool> SetWiredRoomSettingsAsync(
        ActionContext ctx,
        WiredPermissionFlags modifyPermissionMask,
        WiredPermissionFlags readPermissionMask,
        string timezone,
        CancellationToken ct
    );

    /// <summary>
    /// Wired load and capacity figures, or null when the caller may not read the room's wired.
    /// </summary>
    public Task<WiredRoomStatsSnapshot?> GetWiredRoomStatsAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>
    /// The wired monitor's error list, or null when the caller may not read the room's wired.
    /// </summary>
    public Task<ImmutableArray<WiredErrorLogSnapshot>?> GetWiredErrorLogsAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>
    /// Empties the error list. Requires wired modify permission.
    /// </summary>
    public Task<bool> ClearWiredErrorLogsAsync(ActionContext ctx, CancellationToken ct);

    /// <summary>
    /// One variable plus every furni or user currently holding a value for it, or null when the
    /// caller may not read the room wired or the variable is unknown.
    /// </summary>
    public Task<WiredVariableInfoAndHoldersSnapshot?> GetWiredVariableHoldersAsync(
        ActionContext ctx,
        WiredVariableId variableId,
        CancellationToken ct
    );

    /// <summary>
    /// Edits, gives or takes away a variable on one target from the wired menu. Requires wired
    /// modify permission; the target id is a room object id for furni and users.
    /// </summary>
    public Task<bool> ApplyWiredVariableMenuOperationAsync(
        ActionContext ctx,
        WiredVariableBinding binding,
        WiredVariableId variableId,
        WiredVariableMenuOperationType operation,
        WiredVariableValue value,
        CancellationToken ct
    );

    /// <summary>
    /// Takes a stored furni or user variable from everything that holds it, in the room or not.
    /// Requires wired modify permission; false when the caller lacks it.
    /// </summary>
    public Task<bool> RemoveWiredVariableFromAllHoldersAsync(
        ActionContext ctx,
        WiredVariableId variableId,
        CancellationToken ct
    );
}
