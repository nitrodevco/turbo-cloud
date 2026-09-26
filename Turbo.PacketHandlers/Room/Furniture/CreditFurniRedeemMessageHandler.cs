using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Exchanges credit furni for its face value.
/// </summary>
public class CreditFurniRedeemMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CreditFurniRedeemMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        CreditFurniRedeemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new RedeemCreditsInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
