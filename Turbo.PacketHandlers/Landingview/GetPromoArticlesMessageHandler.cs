using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Landingview;

namespace Turbo.PacketHandlers.Landingview;

public class GetPromoArticlesMessageHandler : IMessageHandler<GetPromoArticlesMessage>
{
    public async ValueTask HandleAsync(
        GetPromoArticlesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
