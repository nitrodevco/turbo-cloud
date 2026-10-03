using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;

namespace Turbo.PacketHandlers.Inventory.Badges;

public class GetBadgePointLimitsMessageHandler(IAchievementCatalog catalog, TimeProvider time)
    : IMessageHandler<GetBadgePointLimitsMessage>
{
    public async ValueTask HandleAsync(
        GetBadgePointLimitsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;
        var groups = catalog
            .Current.Where(x => x.IsKnownToClient(time.GetUtcNow().UtcDateTime))
            .Select(x => new BadgePointLimitGroupSnapshot
            {
                BadgeCodePrefix = Regex.Replace(x.Levels[0].BadgeCode, "[0-9]+$", "")[4..],
                Levels = x
                    .Levels.Select(
                        (level, index) =>
                            new BadgePointLimitLevelSnapshot
                            {
                                Level = index + 1,
                                Limit = level.Requirement,
                            }
                    )
                    .ToList(),
            })
            .ToList();
        await ctx.SendComposerAsync(
                new BadgePointLimitsEventMessageComposer { LimitsByBadgeCodePrefix = groups },
                ct
            )
            .ConfigureAwait(false);
    }
}
