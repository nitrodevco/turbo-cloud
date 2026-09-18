using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Friendfurni;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Friendfurni;

/// <summary>
/// One side of a love lock answers the confirmation.
/// </summary>
public class FriendFurniConfirmLockMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<FriendFurniConfirmLockMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        FriendFurniConfirmLockMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ObjectId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .InteractWithItemAsync(
                ctx.AsActionContext(),
                message.ObjectId,
                new ConfirmFriendFurniLockInteraction { Confirmed = message.Confirmed },
                ct
            )
            .ConfigureAwait(false);
    }
}
