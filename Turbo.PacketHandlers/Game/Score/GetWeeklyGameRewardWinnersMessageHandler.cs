using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

public class GetWeeklyGameRewardWinnersMessageHandler
    : IMessageHandler<GetWeeklyGameRewardWinnersMessage>
{
    public async ValueTask HandleAsync(
        GetWeeklyGameRewardWinnersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
