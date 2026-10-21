using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Handshake;

[AllowAnonymous]
public class UniqueIdMessageHandler : IMessageHandler<UniqueIdMessage>
{
    public async ValueTask HandleAsync(
        UniqueIdMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
