using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;

namespace Turbo.PacketHandlers.Moderator;

public class ReleaseIssuesMessageHandler : IMessageHandler<ReleaseIssuesMessage>
{
    public async ValueTask HandleAsync(
        ReleaseIssuesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
