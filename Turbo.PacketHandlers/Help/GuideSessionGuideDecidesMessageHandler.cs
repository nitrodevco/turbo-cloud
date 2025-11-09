using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;

namespace Turbo.PacketHandlers.Help;

public class GuideSessionGuideDecidesMessageHandler
    : IMessageHandler<GuideSessionGuideDecidesMessage>
{
    public async ValueTask HandleAsync(
        GuideSessionGuideDecidesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
