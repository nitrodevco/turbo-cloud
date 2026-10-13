using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// The catalog asks for the bulk-buy ruleset when it opens (<c>HabboCatalog.initBundleDiscounts</c>);
/// Habbo answers with it and Turbo sends the hotel's (<c>Turbo:Catalog:BundleDiscount</c>), which
/// sets the spinner's maximum and the free items its bonus line counts.
/// </summary>
public class GetBundleDiscountRulesetMessageHandler(IOptions<CatalogConfig> catalogConfig)
    : IMessageHandler<GetBundleDiscountRulesetMessage>
{
    private readonly BundleDiscountConfig _config = catalogConfig.Value.BundleDiscount;

    public async ValueTask HandleAsync(
        GetBundleDiscountRulesetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx.SendComposerAsync(
                new BundleDiscountRulesetMessageComposer
                {
                    BundleDiscountRuleset = _config.ToSnapshot(),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
