using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Groupforums;

namespace Turbo.PacketHandlers.Groupforums;

public class ModerateThreadMessageHandler : IMessageHandler<ModerateThreadMessage>
{
    public async ValueTask HandleAsync(
        ModerateThreadMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
