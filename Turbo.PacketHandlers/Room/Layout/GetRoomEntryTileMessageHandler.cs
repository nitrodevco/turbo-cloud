using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Layout;

namespace Turbo.PacketHandlers.Room.Layout;

public class GetRoomEntryTileMessageHandler : IMessageHandler<GetRoomEntryTileMessage>
{
    public async ValueTask HandleAsync(
        GetRoomEntryTileMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
