using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>The renter, or the room owner, ends a rent early.</summary>
public class RentableSpaceCancelRentMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RentableSpaceCancelRentMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RentableSpaceCancelRentMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new CancelSpaceRentInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
