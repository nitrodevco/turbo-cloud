using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Walks the avatar through a one-way gate from the tile in front of it.
/// </summary>
public class EnterOneWayDoorMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<EnterOneWayDoorMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        EnterOneWayDoorMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new EnterOneWayDoorInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
