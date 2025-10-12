using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;

namespace Turbo.Authentication.Handlers;

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
