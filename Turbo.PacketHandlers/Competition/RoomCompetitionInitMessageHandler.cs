using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Competition;

namespace Turbo.PacketHandlers.Competition;

public class RoomCompetitionInitMessageHandler : IMessageHandler<RoomCompetitionInitMessage>
{
    public async ValueTask HandleAsync(
        RoomCompetitionInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
