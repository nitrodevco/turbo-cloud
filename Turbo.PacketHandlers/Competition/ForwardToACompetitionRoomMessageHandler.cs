using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Competition;

namespace Turbo.PacketHandlers.Competition;

public class ForwardToACompetitionRoomMessageHandler
    : IMessageHandler<ForwardToACompetitionRoomMessage>
{
    public async ValueTask HandleAsync(
        ForwardToACompetitionRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
