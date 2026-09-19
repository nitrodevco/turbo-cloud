using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public async Task OnPlayerUpdatedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct)
    {
        if (_state.ActiveRoomId > 0)
        {
            var room = _grainFactory.GetRoomGrain(_state.ActiveRoomId);

            await room.UpdateAvatarWithPlayerAsync(snapshot, ct);
        }

        _grainFactory
            .GetPlayerMessengerGrain(_state.PlayerId)
            .UpdateFriendsAsync(snapshot, ct)
            .LogAndForget(_logger, $"update friends of player {_state.PlayerId}");
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
