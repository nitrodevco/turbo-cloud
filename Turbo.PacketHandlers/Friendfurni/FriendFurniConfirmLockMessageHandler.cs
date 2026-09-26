using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Friendfurni;

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
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new ConfirmFriendFurniLockInteraction { Confirmed = message.Confirmed },
                ct
            )
            .ConfigureAwait(false);
    }
}
