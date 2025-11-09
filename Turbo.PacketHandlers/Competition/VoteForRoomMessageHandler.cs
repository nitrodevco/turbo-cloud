using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Competition;

namespace Turbo.PacketHandlers.Competition;

public class VoteForRoomMessageHandler : IMessageHandler<VoteForRoomMessage>
{
    public async ValueTask HandleAsync(
        VoteForRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
