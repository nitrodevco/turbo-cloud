using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public Task OnPermissionsChangedAsync(
        ResolvedPermissionsSnapshot permissions,
        CancellationToken ct
    )
    {
        if (_state.ActiveRoomId <= 0)
            return Task.CompletedTask;

        // Told, never awaited: the room may be waiting on the permission grain, which is waiting
        // on this call, to answer a rights check for somebody walking in.
        _grainFactory
            .GetRoomGrain(_state.ActiveRoomId)
            .SetPlayerPermissionsAsync(_state.PlayerId, permissions, CancellationToken.None)
            .LogAndForget(
                _logger,
                "tell room {RoomId} about the permissions of player {PlayerId}",
                _state.ActiveRoomId,
                _state.PlayerId
            );

        return Task.CompletedTask;
    }
}
