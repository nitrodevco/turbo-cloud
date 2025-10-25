using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Handshake;

namespace Turbo.PacketHandlers.Handshake;

public class InfoRetrieveMessageHandler : IMessageHandler<InfoRetrieveMessage>
{
    public async ValueTask HandleAsync(
        InfoRetrieveMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
