using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// The moodlight editor opens; the item answers with its presets.
/// </summary>
public class RoomDimmerGetPresetsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RoomDimmerGetPresetsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RoomDimmerGetPresetsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new RequestDimmerPresetsInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
