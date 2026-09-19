using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Badges;

public class RequestABadgeMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RequestABadgeMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RequestABadgeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // Only a badge the hotel lists for the request code can be claimed. The request code is
        // the client's to send; the badge it stands for never is.
        var badgeCode = await _grainFactory
            .GetBadgeDirectoryGrain()
            .GetRequestableBadgeAsync(message.RequestCode, ct)
            .ConfigureAwait(false);

        if (badgeCode is null)
            return;

        var inventory = _grainFactory.GetInventoryGrain(ctx.PlayerId);

        await inventory.GiveBadgeAsync(badgeCode, ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IsBadgeRequestFulfilledEventMessageComposer
                {
                    RequestCode = message.RequestCode,
                    Fulfilled = await inventory.HasBadgeAsync(badgeCode, ct).ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
