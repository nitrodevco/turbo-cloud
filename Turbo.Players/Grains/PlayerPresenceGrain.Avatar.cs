using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public Task OnPlayerUpdatedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct)
    {
        // Told, not awaited, like everything this grain passes on: the player grain awaits this
        // call, and the room may be waiting on that player grain (respect, a mannequin).
        if (_state.ActiveRoomId > 0)
            _grainFactory
                .GetRoomGrain(_state.ActiveRoomId)
                .UpdateAvatarWithPlayerAsync(snapshot, CancellationToken.None)
                .LogAndForget(
                    _logger,
                    $"show the update of player {_state.PlayerId} in room {_state.ActiveRoomId}"
                );

        _grainFactory
            .GetPlayerMessengerGrain(_state.PlayerId)
            .UpdateFriendsAsync(snapshot, CancellationToken.None)
            .LogAndForget(_logger, $"update friends of player {_state.PlayerId}");

        return Task.CompletedTask;
    }

    public Task OnBadgesRankChangedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct)
    {
        if (_state.ActiveRoomId <= 0)
            return Task.CompletedTask;

        // Told, not awaited, for the reason worn badges are: the room may be waiting on this
        // player's inventory while the inventory waits on us.
        _grainFactory
            .GetRoomGrain(_state.ActiveRoomId)
            .UpdateAvatarWithPlayerAsync(snapshot, CancellationToken.None)
            .LogAndForget(
                _logger,
                $"show the badges rank of player {_state.PlayerId} in room {_state.ActiveRoomId}"
            );

        return Task.CompletedTask;
    }

    public async Task OnFigureUpdatedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct)
    {
        await SendComposerAsync(
            new FigureUpdateEventMessageComposer
            {
                Figure = snapshot.Figure,
                Gender = snapshot.Gender,
            },
            ct
        );

        await OnPlayerUpdatedAsync(snapshot, ct);
    }
}
