using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Nudges a magic stack tile to the next item height above or below.
/// </summary>
public class SetAdjacentCustomStackingHeightMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetAdjacentCustomStackingHeightMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetAdjacentCustomStackingHeightMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SetAdjacentStackHeightInteraction { Down = message.Down },
                ct
            )
            .ConfigureAwait(false);
    }
}
