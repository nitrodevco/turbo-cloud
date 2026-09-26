using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
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

        if (badgeCode is not null)
            await _grainFactory
                .GetInventoryGrain(ctx.PlayerId)
                .GiveBadgeAsync(badgeCode, ct)
                .ConfigureAwait(false);

        await ctx.SendBadgeRequestFulfilledAsync(_grainFactory, message.RequestCode, badgeCode, ct)
            .ConfigureAwait(false);
    }
}
