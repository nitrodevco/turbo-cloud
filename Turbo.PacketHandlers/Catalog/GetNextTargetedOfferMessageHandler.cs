using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// Turbo has no targeted offers, so it answers as Habbo does when there is none for the player:
/// an empty TargetedOfferNotFound, which the client takes as "no offer" and stops waiting.
/// </summary>
public class GetNextTargetedOfferMessageHandler : IMessageHandler<GetNextTargetedOfferMessage>
{
    public async ValueTask HandleAsync(
        GetNextTargetedOfferMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(new TargetedOfferNotFoundEventMessageComposer(), ct)
            .ConfigureAwait(false);
    }
}
