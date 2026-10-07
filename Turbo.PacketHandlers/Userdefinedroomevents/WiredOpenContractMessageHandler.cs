using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>Asks for a wired contract's contents; the contract decides who may read it.</summary>
public class WiredOpenContractMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredOpenContractMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredOpenContractMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.ContractId,
                new OpenContractInteraction(),
                ct
            )
            .ConfigureAwait(false);
    }
}
