using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>The text typed into a WIRED label's bubble.</summary>
public class SetFurnitureLabelDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetFurnitureLabelDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetFurnitureLabelDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ObjectId,
                new SetFurnitureLabelTextInteraction { Text = message.Text },
                ct
            )
            .ConfigureAwait(false);
    }
}
