using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Sent by a catalog pet page when the buyer confirms a name, before the purchase. The inventory
/// answers with the rules a bought pet's name is held to; the client opens the purchase
/// confirmation on an approval and shows the reason otherwise.
/// </summary>
public class ApproveNameMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ApproveNameMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ApproveNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // The client asks for pet names only; there is no other kind to approve.
        if (message.Type != ApproveNameType.Pet)
            return;

        await _grainFactory
            .GetInventoryGrain(ctx.PlayerId)
            .SendPetNameApprovalAsync(message.Name, ct)
            .ConfigureAwait(false);
    }
}
