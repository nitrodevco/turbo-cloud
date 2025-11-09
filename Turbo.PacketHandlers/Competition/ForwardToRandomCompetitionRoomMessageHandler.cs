using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Competition;

namespace Turbo.PacketHandlers.Competition;

public class ForwardToRandomCompetitionRoomMessageHandler
    : IMessageHandler<ForwardToRandomCompetitionRoomMessage>
{
    public async ValueTask HandleAsync(
        ForwardToRandomCompetitionRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
