using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;

namespace Turbo.PacketHandlers.Users;

public class UpdateGuildBadgeMessageHandler : IMessageHandler<UpdateGuildBadgeMessage>
{
    public async ValueTask HandleAsync(
        UpdateGuildBadgeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
