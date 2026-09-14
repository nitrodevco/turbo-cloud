using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
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
}
