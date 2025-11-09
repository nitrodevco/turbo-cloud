using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Help;

namespace Turbo.PacketHandlers.Help;

public class GuideSessionInviteRequesterMessageHandler
    : IMessageHandler<GuideSessionInviteRequesterMessage>
{
    public async ValueTask HandleAsync(
        GuideSessionInviteRequesterMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
