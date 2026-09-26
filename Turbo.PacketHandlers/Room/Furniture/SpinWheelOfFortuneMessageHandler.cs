using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Spins the wheel of fortune.
/// </summary>
public class SpinWheelOfFortuneMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SpinWheelOfFortuneMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SpinWheelOfFortuneMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SpinWheelInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
