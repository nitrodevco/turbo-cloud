using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;

namespace Turbo.PacketHandlers.Moderator;

public class CloseIssuesMessageHandler : IMessageHandler<CloseIssuesMessage>
{
    public async ValueTask HandleAsync(
        CloseIssuesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
