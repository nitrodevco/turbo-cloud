using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Lobby;

namespace Turbo.PacketHandlers.Game.Lobby;

public class GetUserGameAchievementsMessageHandler : IMessageHandler<GetUserGameAchievementsMessage>
{
    public async ValueTask HandleAsync(
        GetUserGameAchievementsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
