using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Handshake;

[AllowAnonymous]
public class VersionCheckMessageHandler : IMessageHandler<VersionCheckMessage>
{
    public async ValueTask HandleAsync(
        VersionCheckMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
