using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>Rents a rentable space; the furni checks the price and the one-space rule.</summary>
public class RentableSpaceRentMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RentableSpaceRentMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RentableSpaceRentMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new RentSpaceInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
