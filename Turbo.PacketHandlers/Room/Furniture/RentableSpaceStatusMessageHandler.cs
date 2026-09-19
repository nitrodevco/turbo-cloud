using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>The rentable space window asks who rents the space and what it costs.</summary>
public class RentableSpaceStatusMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RentableSpaceStatusMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RentableSpaceStatusMessage message,
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
                new RequestRentableSpaceStatusInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
