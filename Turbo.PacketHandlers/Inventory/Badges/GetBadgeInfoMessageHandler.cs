using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Badges;

public class GetBadgeInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetBadgeInfoMessage>
{
    private const int NOT_OWNED = 0;

    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetBadgeInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || string.IsNullOrWhiteSpace(message.BadgeCode))
            return;

        // Independent grains: the hotel-wide figures for the badge, and whether the asker owns it.
        var infoTask = _grainFactory.GetBadgeDirectoryGrain().GetInfoAsync([message.BadgeCode], ct);
        var ownedTask = _grainFactory
            .GetInventoryGrain(ctx.PlayerId)
            .GetBadgeSnapshotAsync(message.BadgeCode, ct);

        await Task.WhenAll(infoTask, ownedTask).ConfigureAwait(false);

        var owned = await ownedTask.ConfigureAwait(false);
        var info = await infoTask.ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new BadgeInfoMessageComposer
                {
                    BadgeId = owned?.BadgeId ?? NOT_OWNED,
                    Info = info[0],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
