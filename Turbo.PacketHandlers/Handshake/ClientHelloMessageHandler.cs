using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;

namespace Turbo.PacketHandlers.Handshake;

public class ClientHelloMessageHandler() : IMessageHandler<ClientHelloMessage>
{
    public async ValueTask HandleAsync(
        ClientHelloMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (message.Production is null)
        {
            await ctx.CloseSessionAsync().ConfigureAwait(false);

            return;
        }

        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
