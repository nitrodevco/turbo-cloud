using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.GroupForums;

namespace Turbo.PacketHandlers.GroupForums;

public class GetUnreadForumsCountMessageHandler : IMessageHandler<GetUnreadForumsCountMessage>
{
    public async ValueTask HandleAsync(
        GetUnreadForumsCountMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
