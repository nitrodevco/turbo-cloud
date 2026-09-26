using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Switches the moodlight on or off.
/// </summary>
public class RoomDimmerChangeStateMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RoomDimmerChangeStateMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RoomDimmerChangeStateMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new ToggleDimmerInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
