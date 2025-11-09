using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Hotlooks;

namespace Turbo.PacketHandlers.Hotlooks;

public class GetHotLooksMessageHandler : IMessageHandler<GetHotLooksMessage>
{
    public async ValueTask HandleAsync(
        GetHotLooksMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
