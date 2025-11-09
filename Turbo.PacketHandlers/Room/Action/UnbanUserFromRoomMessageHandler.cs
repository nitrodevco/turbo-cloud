using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;

namespace Turbo.PacketHandlers.Room.Action;

public class UnbanUserFromRoomMessageHandler : IMessageHandler<UnbanUserFromRoomMessage>
{
    public async ValueTask HandleAsync(
        UnbanUserFromRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
