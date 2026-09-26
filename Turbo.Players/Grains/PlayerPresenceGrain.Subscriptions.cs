using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public Task OnHabboClubChangedAsync(DateTime? expiresAt, CancellationToken ct)
    {
        if (_state.ActiveRoomId <= 0)
            return Task.CompletedTask;

        // Told, never awaited: the subscription grain is waiting on this call, and the room may
        // be waiting on that same subscription grain to answer a placement.
        _grainFactory
            .GetRoomGrain(_state.ActiveRoomId)
            .SetPlayerHabboClubAsync(_state.PlayerId, expiresAt, CancellationToken.None)
            .LogAndForget(
                _logger,
                $"tell room {_state.ActiveRoomId} about the Habbo Club of player {_state.PlayerId}"
            );

        return Task.CompletedTask;
    }
}
