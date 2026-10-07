using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>Saves a wired contract; the contract checks who may and what it may hold.</summary>
public class WiredUpdateContractMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredUpdateContractMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredUpdateContractMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.InteractWithRoomItemAsync(
                _grainFactory,
                message.Contract.ContractId,
                new UpdateContractInteraction { Contract = message.Contract },
                ct
            )
            .ConfigureAwait(false);
    }
}
