using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Competition;

namespace Turbo.PacketHandlers.Competition;

public class GetSecondsUntilMessageHandler : IMessageHandler<GetSecondsUntilMessage>
{
    public async ValueTask HandleAsync(
        GetSecondsUntilMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
