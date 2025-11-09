using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nux;

namespace Turbo.PacketHandlers.Nux;

public class SelectInitialRoomMessageHandler : IMessageHandler<SelectInitialRoomMessage>
{
    public async ValueTask HandleAsync(
        SelectInitialRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
