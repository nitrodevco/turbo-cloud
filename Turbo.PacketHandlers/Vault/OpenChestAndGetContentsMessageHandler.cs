using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>Opens a wired chest's window; the chest decides who may look and sends its contents.</summary>
public class OpenChestAndGetContentsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<OpenChestAndGetContentsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        OpenChestAndGetContentsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new OpenChestInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
