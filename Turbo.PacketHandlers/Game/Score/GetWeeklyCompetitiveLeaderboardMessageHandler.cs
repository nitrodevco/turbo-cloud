using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

public class GetWeeklyCompetitiveLeaderboardMessageHandler
    : IMessageHandler<GetWeeklyCompetitiveLeaderboardMessage>
{
    public async ValueTask HandleAsync(
        GetWeeklyCompetitiveLeaderboardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
