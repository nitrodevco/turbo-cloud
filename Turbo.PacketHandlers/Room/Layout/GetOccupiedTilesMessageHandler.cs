using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Layout;

namespace Turbo.PacketHandlers.Room.Layout;

public class GetOccupiedTilesMessageHandler : IMessageHandler<GetOccupiedTilesMessage>
{
    public async ValueTask HandleAsync(
        GetOccupiedTilesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
