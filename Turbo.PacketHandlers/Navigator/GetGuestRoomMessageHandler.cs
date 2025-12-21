using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Navigator;

public class GetGuestRoomMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetGuestRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetGuestRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var roomGrain = _grainFactory.GetRoomGrain(message.RoomId);
        var snapshot = await roomGrain.GetSnapshotAsync().ConfigureAwait(false);

        var staffPick = false;
        var groupMember = false;
        var allInRoomMuted = false;
        var canMute = false;

        await ctx.SendComposerAsync(
                new GetGuestRoomResultMessageComposer
                {
                    EnterRoom = message.EnterRoom,
                    RoomInfo = snapshot,
                    RoomForward = message.RoomForward,
                    StaffPick = staffPick,
                    IsGroupMember = groupMember,
                    AllInRoomMuted = allInRoomMuted,
                    CanMute = canMute,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
