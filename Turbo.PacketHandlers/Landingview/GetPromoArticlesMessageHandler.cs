using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Messages.Incoming.Landingview;
using Turbo.Primitives.Messages.Outgoing.Landingview;

namespace Turbo.PacketHandlers.Landingview;

public class GetPromoArticlesMessageHandler(IPromoArticleService articles)
    : IMessageHandler<GetPromoArticlesMessage>
{
    public async ValueTask HandleAsync(
        GetPromoArticlesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new PromoArticlesMessageComposer
                {
                    Articles = await articles.GetLiveAsync(ct).ConfigureAwait(false),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
