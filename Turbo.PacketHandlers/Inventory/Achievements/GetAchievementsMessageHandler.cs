using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Achievements;

namespace Turbo.PacketHandlers.Inventory.Achievements;

public class GetAchievementsMessageHandler : IMessageHandler<GetAchievementsMessage>
{
    public async ValueTask HandleAsync(
        GetAchievementsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
