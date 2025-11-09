using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Roomdirectory;

namespace Turbo.PacketHandlers.Roomdirectory;

public class RoomNetworkOpenConnectionMessageHandler
    : IMessageHandler<RoomNetworkOpenConnectionMessage>
{
    public async ValueTask HandleAsync(
        RoomNetworkOpenConnectionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
