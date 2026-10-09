using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

public class GetCatalogPageWithEarliestExpiryMessageHandler(
    IExpiringPageService expiringPages,
    TimeProvider time
) : IMessageHandler<GetCatalogPageWithEarliestExpiryMessage>
{
    public async ValueTask HandleAsync(
        GetCatalogPageWithEarliestExpiryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var page = await expiringPages.GetEarliestAsync(ct).ConfigureAwait(false);

        // No page counts down: an empty name, which the widget hides itself for.
        await ctx.SendComposerAsync(
                new CatalogPageWithEarliestExpiryMessageComposer
                {
                    PageName = page?.PageName ?? string.Empty,
                    SecondsToExpiry = page is null
                        ? 0
                        : (int)
                            Math.Clamp(
                                Math.Ceiling(
                                    (page.ExpiresAt - time.GetUtcNow().UtcDateTime).TotalSeconds
                                ),
                                0,
                                int.MaxValue
                            ),
                    Image = page?.Image ?? string.Empty,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
