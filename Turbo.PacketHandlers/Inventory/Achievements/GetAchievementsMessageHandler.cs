using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Orleans;
using Turbo.Primitives.Messages.Incoming.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;

namespace Turbo.PacketHandlers.Inventory.Achievements;

public class GetAchievementsMessageHandler(IGrainFactory grains, IAchievementCatalog catalog)
    : IMessageHandler<GetAchievementsMessage>
{
    public async ValueTask HandleAsync(
        GetAchievementsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;
        var achievements = await grains
            .GetPlayerAchievementGrain(ctx.PlayerId)
            .GetAchievementsAsync(ct)
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new AchievementsEventMessageComposer
                {
                    Achievements = achievements,
                    DefaultCategory = catalog.DefaultCategory,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
