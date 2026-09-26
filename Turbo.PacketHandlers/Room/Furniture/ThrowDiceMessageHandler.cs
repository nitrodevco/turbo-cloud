using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Double-click on a closed dice: it rolls and lands on a face after the configured delay.
/// </summary>
public class ThrowDiceMessageHandler(IGrainFactory grainFactory) : IMessageHandler<ThrowDiceMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ThrowDiceMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new ThrowDiceInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
