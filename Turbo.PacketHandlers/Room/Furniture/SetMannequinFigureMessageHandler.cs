using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Dresses the mannequin in what the owner is wearing.
/// </summary>
public class SetMannequinFigureMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetMannequinFigureMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetMannequinFigureMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SetMannequinFigureInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
