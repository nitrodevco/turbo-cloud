using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class RemoveOwnRoomRightsRoomMessageHandler : IMessageHandler<RemoveOwnRoomRightsRoomMessage>
{
    public async ValueTask HandleAsync(
        RemoveOwnRoomRightsRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
