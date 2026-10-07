using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>Takes furni of one type out of a furni chest.</summary>
public class WithdrawItemsFromChestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WithdrawItemsFromChestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WithdrawItemsFromChestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.Amount <= 0)
            return;

        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new WithdrawFromChestInteraction
                {
                    Amount = message.Amount,
                    ItemType = message.ItemType,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
