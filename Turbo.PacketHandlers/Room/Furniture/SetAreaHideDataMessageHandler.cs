using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Configures the area an area hider covers.
/// </summary>
public class SetAreaHideDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetAreaHideDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetAreaHideDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SetAreaHideInteraction
                {
                    RootX = message.RootX,
                    RootY = message.RootY,
                    Width = message.Width,
                    Length = message.Length,
                    Invisibility = message.Invisibility,
                    WallItems = message.WallItems,
                    Invert = message.Invert,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
