using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Sets a magic stack tile height; the multi-walk flag is not modelled yet.
/// </summary>
public class SetCustomStackingHeightMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetCustomStackingHeightMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetCustomStackingHeightMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SetStackHeightInteraction { Height = message.Height },
                ct
            )
            .ConfigureAwait(false);
    }
}
