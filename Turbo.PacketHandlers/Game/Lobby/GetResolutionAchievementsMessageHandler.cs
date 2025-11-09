using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Game.Lobby;

namespace Turbo.PacketHandlers.Game.Lobby;

public class GetResolutionAchievementsMessageHandler
    : IMessageHandler<GetResolutionAchievementsMessage>
{
    public async ValueTask HandleAsync(
        GetResolutionAchievementsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
