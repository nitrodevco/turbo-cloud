using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
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
