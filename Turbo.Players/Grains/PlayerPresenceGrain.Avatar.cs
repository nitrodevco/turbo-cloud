using System.Threading;
using System.Threading.Tasks;
using Orleans;
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
            .Ignore();
    }

    public async Task OnFigureUpdatedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct)
    {
        await SendComposerAsync(
            new FigureUpdateEventMessageComposer
            {
                Figure = snapshot.Figure,
                Gender = snapshot.Gender,
            }
        );

        await OnPlayerUpdatedAsync(snapshot, ct);
    }
}
