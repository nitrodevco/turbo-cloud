using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Score;

namespace Turbo.PacketHandlers.Game.Score;

public class GetWeeklyGameRewardMessageHandler : IMessageHandler<GetWeeklyGameRewardMessage>
{
    public async ValueTask HandleAsync(
        GetWeeklyGameRewardMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
