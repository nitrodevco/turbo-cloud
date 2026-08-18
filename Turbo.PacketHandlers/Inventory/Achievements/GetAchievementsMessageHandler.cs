using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Achievements;

public class GetAchievementsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetAchievementsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetAchievementsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var achievementGrain = _grainFactory.GetPlayerAchievementGrain(ctx.PlayerId);

        var achievements = await achievementGrain.GetAllProgressAsync(ct).ConfigureAwait(false);
        var score = await achievementGrain.GetTotalScoreAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new AchievementsEventMessageComposer { Achievements = achievements },
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new AchievementsScoreEventMessageComposer { Score = score },
                ct
            )
            .ConfigureAwait(false);
    }
}
