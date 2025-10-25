using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class CompetitionRoomsSearchMessageHandler : IMessageHandler<CompetitionRoomsSearchMessage>
{
    public async ValueTask HandleAsync(
        CompetitionRoomsSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
