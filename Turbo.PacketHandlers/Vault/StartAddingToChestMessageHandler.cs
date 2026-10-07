using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Vault;

namespace Turbo.PacketHandlers.Vault;

/// <summary>Opens the trade window to put things into a chest, if the chest lets this player.</summary>
public class StartAddingToChestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<StartAddingToChestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        StartAddingToChestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ChestId,
                new StartChestDepositInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
