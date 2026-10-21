using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<ImmutableArray<WiredVariableSnapshot>> GetSharedWiredVariablesAsync(
        CancellationToken ct
    ) => WiredSystem.GetSharedVariablesAsync(ct);

    public Task<SharedWiredVariableStateSnapshot?> SubscribeSharedWiredVariableAsync(
        WiredVariableId variableId,
        RoomId referrer,
        CancellationToken ct
    ) => WiredSystem.SubscribeSharedVariableAsync(variableId, referrer, ct);

    public Task<bool> ChangeSharedWiredVariableAsync(
        WiredVariableId variableId,
        SharedWiredVariableChange change,
        RoomId origin,
        CancellationToken ct
    ) => WiredSystem.ApplySharedVariableChangeAsync(variableId, change, origin, ct);

    public async Task<bool> SynchronizeSharedWiredAddonsAsync(
        ActionContext ctx,
        int boxId,
        CancellationToken ct
    )
    {
        // Whoever may edit the room's wired may synchronize (the button is disabled without
        // wiredMenu.hasWritePermission, VariableAddonShareAddon.onEditInitialized).
        var level = await SecurityModule.GetControllerLevelAsync(ctx);
        var (canModify, _) = WiredSystem.GetPermissions(level);

        return canModify && await WiredSystem.SynchronizeSharedAddonsAsync(boxId, ct);
    }

    public Task OnSharedWiredVariableAddonsChangedAsync(
        RoomId sourceRoom,
        WiredVariableId variableId,
        CancellationToken ct
    ) => WiredSystem.OnSharedVariableAddonsChangedAsync(sourceRoom, variableId, ct);

    public Task OnSharedWiredVariableChangedAsync(
        RoomId sourceRoom,
        WiredVariableId variableId,
        SharedWiredVariableChange change,
        CancellationToken ct
    )
    {
        WiredSystem.OnSharedVariableChanged(sourceRoom, variableId, change);

        return Task.CompletedTask;
    }
}
