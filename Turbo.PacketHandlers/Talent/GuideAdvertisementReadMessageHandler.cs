using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Talent;

namespace Turbo.PacketHandlers.Talent;

public class GuideAdvertisementReadMessageHandler : IMessageHandler<GuideAdvertisementReadMessage>
{
    public async ValueTask HandleAsync(
        GuideAdvertisementReadMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
