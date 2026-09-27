using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Players.Grains;

/// <summary>
/// The three inventory sections as the client sees them. Furniture and pets are sent in
/// fragments; bots fit one packet. An empty section still answers, so the client stops waiting.
/// </summary>
internal sealed partial class PlayerPresenceGrain
{
    public Task OnSelectedBadgesChangedAsync(
        ImmutableArray<PlayerBadgeSnapshot> selectedBadges,
        CancellationToken ct
    )
    {
        if (_state.ActiveRoomId <= 0)
            return SendComposerAsync(
                new HabboUserBadgesMessageComposer
                {
                    PlayerId = _state.PlayerId,
                    Badges = selectedBadges,
                },
                ct
            );

        // The room shows the change to everyone in it, this player included. It is told, not
        // awaited: the room may be waiting on this player's inventory, which is waiting on us.
        _grainFactory
            .GetRoomGrain(_state.ActiveRoomId)
            .SetPlayerBadgesAsync(_state.PlayerId, selectedBadges, CancellationToken.None)
            .LogAndForget(
                _logger,
                "show the badges of player {PlayerId} in room {RoomId}",
                _state.PlayerId,
                _state.ActiveRoomId
            );

        return Task.CompletedTask;
    }
}
