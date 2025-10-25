using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class ForwardToARandomPromotedRoomMessageHandler
    : IMessageHandler<ForwardToARandomPromotedRoomMessage>
{
    public async ValueTask HandleAsync(
        ForwardToARandomPromotedRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
