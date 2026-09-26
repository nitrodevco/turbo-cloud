using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
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

        await ctx.SendBadgeRequestFulfilledAsync(_grainFactory, message.RequestCode, badgeCode, ct)
            .ConfigureAwait(false);
    }
}
