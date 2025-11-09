using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Gifts;

namespace Turbo.PacketHandlers.Gifts;

public class VerifyCodeMessageHandler : IMessageHandler<VerifyCodeMessage>
{
    public async ValueTask HandleAsync(
        VerifyCodeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
