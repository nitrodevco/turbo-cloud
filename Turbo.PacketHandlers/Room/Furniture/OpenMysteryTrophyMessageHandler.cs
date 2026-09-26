using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Engraves a mystery trophy with the owner's inscription.
/// </summary>
public class OpenMysteryTrophyMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<OpenMysteryTrophyMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        OpenMysteryTrophyMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new EngraveTrophyInteraction { Inscription = message.Inscription },
                ct
            )
            .ConfigureAwait(false);
    }
}
