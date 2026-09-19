using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetBadgeLeaderboardMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetBadgeLeaderboardMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetBadgeLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // The leaderboard grain bounds the type, tier, chunk and size; they arrive here unchecked.
        var page = await _grainFactory
            .GetBadgeLeaderboardGrain()
            .GetLeaderboardAsync(
                (BadgeLeaderboardType)message.Type,
                message.Rarity,
                message.ChunkIndex,
                message.ChunkSize,
                ctx.PlayerId,
                ct
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new BadgeLeaderboardResultMessageComposer { Page = page }, ct)
            .ConfigureAwait(false);
    }
}
