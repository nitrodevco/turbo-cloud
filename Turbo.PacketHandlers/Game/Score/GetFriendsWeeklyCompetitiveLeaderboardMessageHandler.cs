using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

public class GetFriendsWeeklyCompetitiveLeaderboardMessageHandler
    : IMessageHandler<GetFriendsWeeklyCompetitiveLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        GetFriendsWeeklyCompetitiveLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
