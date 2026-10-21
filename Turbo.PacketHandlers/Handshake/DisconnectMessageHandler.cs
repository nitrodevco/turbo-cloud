using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Handshake;

[AllowAnonymous]
public class DisconnectMessageHandler : IMessageHandler<DisconnectMessage>
{
    public async ValueTask HandleAsync(
        DisconnectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
