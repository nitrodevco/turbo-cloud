using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;

namespace Turbo.PacketHandlers.Moderator;

public class ModToolSanctionMessageHandler : IMessageHandler<ModToolSanctionMessage>
{
    public async ValueTask HandleAsync(
        ModToolSanctionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
