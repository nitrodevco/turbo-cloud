using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class MyRoomRightsSearchMessageHandler : IMessageHandler<MyRoomRightsSearchMessage>
{
    public async ValueTask HandleAsync(
        MyRoomRightsSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
