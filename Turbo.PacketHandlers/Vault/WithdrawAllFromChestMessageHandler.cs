using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>Takes everything out of a chest.</summary>
public class WithdrawAllFromChestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WithdrawAllFromChestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WithdrawAllFromChestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new WithdrawFromChestInteraction { Amount = null, ItemType = null },
                ct
            )
            .ConfigureAwait(false);
    }
}
