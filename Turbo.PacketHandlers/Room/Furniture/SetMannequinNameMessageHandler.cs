using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Names the mannequin outfit.
/// </summary>
public class SetMannequinNameMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetMannequinNameMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetMannequinNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SetMannequinNameInteraction { Name = message.Name },
                ct
            )
            .ConfigureAwait(false);
    }
}
