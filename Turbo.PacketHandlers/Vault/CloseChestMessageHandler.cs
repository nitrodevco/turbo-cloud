using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>The chest's window was closed; it stops sending changes.</summary>
public class CloseChestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<CloseChestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        CloseChestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new CloseChestInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
