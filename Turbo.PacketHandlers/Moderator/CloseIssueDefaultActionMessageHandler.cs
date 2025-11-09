using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;

namespace Turbo.PacketHandlers.Moderator;

public class CloseIssueDefaultActionMessageHandler : IMessageHandler<CloseIssueDefaultActionMessage>
{
    public async ValueTask HandleAsync(
        CloseIssueDefaultActionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
