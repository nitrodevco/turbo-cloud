using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Badges;

public class GetIsBadgeRequestFulfilledMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetIsBadgeRequestFulfilledMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetIsBadgeRequestFulfilledMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var badgeCode = await _grainFactory
            .GetBadgeDirectoryGrain()
            .GetRequestableBadgeAsync(message.RequestCode, ct)
            .ConfigureAwait(false);

        var fulfilled =
            badgeCode is not null
            && await _grainFactory
                .GetInventoryGrain(ctx.PlayerId)
                .HasBadgeAsync(badgeCode, ct)
                .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IsBadgeRequestFulfilledEventMessageComposer
                {
                    RequestCode = message.RequestCode,
                    Fulfilled = fulfilled,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
