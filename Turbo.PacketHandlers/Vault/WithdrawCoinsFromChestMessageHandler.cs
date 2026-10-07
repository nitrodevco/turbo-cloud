using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>Takes credits out of a credit chest.</summary>
public class WithdrawCoinsFromChestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WithdrawCoinsFromChestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WithdrawCoinsFromChestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.Amount <= 0)
            return;

        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new WithdrawFromChestInteraction { Amount = message.Amount, ItemType = null },
                ct
            )
            .ConfigureAwait(false);
    }
}
