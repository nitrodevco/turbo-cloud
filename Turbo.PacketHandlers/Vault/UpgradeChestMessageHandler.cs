using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>Buys capacity for a chest; the chest's grain checks the owner, the limit and the price.</summary>
public class UpgradeChestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UpgradeChestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UpgradeChestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new UpgradeChestInteraction { Upgrades = message.UpgradeCount },
                ct
            )
            .ConfigureAwait(false);
    }
}
