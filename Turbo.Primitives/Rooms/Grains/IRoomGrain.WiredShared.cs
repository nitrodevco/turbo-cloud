using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>
/// Shared wired variables ("Permanent, shared"), between the room a variable lives in and the
/// rooms that use it through "WIRED Variable: From Another Room". The reads interleave: a room
/// asks while it loads its own boxes, and two rooms using each other's variables would otherwise
/// wait on each other.
/// </summary>
public partial interface IRoomGrain
{
    /// <summary>The shared user and global variables of this room.</summary>
    [AlwaysInterleave]
    public Task<ImmutableArray<WiredVariableSnapshot>> GetSharedWiredVariablesAsync(
        CancellationToken ct
    );

    /// <summary>
    /// Every value of one of this room's shared variables, and from now on its changes are sent
    /// to <paramref name="referrer"/>. Null when the room has no such shared variable.
    /// </summary>
    [AlwaysInterleave]
    public Task<SharedWiredVariableStateSnapshot?> SubscribeSharedWiredVariableAsync(
        WiredVariableId variableId,
        RoomId referrer,
        CancellationToken ct
    );

    /// <summary>
    /// A change another room's wired made to one of this room's shared variables. It is applied
    /// here, where the value is kept, and passed on to every other room using the variable.
    /// </summary>
    public Task<bool> ChangeSharedWiredVariableAsync(
        WiredVariableId variableId,
        SharedWiredVariableChange change,
        RoomId origin,
        CancellationToken ct
    );

    /// <summary>A shared variable of <paramref name="sourceRoom"/> that this room uses changed.</summary>
    public Task OnSharedWiredVariableChangedAsync(
        RoomId sourceRoom,
        WiredVariableId variableId,
        SharedWiredVariableChange change,
        CancellationToken ct
    );
}
