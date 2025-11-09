using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Competition;

namespace Turbo.PacketHandlers.Competition;

public class ForwardToASubmittableRoomMessageHandler
    : IMessageHandler<ForwardToASubmittableRoomMessage>
{
    public async ValueTask HandleAsync(
        ForwardToASubmittableRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
