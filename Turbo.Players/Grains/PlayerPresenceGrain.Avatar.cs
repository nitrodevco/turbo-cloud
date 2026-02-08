using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Snapshots.Players;

namespace Turbo.Players.Grains;

internal sealed partial class PlayerPresenceGrain
{
    public async Task OnFigureUpdatedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct)
    {
        await SendComposerAsync(
            new FigureUpdateEventMessageComposer
            {
                Figure = snapshot.Figure,
                Gender = snapshot.Gender,
            }
        );

        if (_state.ActiveRoomId > 0)
        {
            await SendComposerAsync(
                new UserChangeMessageComposer
                {
                    ObjectId = -1,
                    Figure = snapshot.Figure,
                    Gender = snapshot.Gender,
                    CustomInfo = snapshot.Motto,
                    AchievementScore = snapshot.AchievementScore,
                }
            );

            var room = _grainFactory.GetRoomGrain(_state.ActiveRoomId);

            await room.UpdateAvatarWithPlayerAsync(snapshot, ct);
        }
    }
}
