using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Providers;

/// <summary>
/// Reads the configured reward and persisted purchase progress for BonusRarePromoWidget.
/// This is informational: requesting the widget never credits currency or awards furniture.
/// </summary>
internal sealed class BonusRareProvider(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<CatalogConfig> options,
    IFurnitureDefinitionProvider definitions
) : IBonusRareProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly BonusRareConfig _config = options.Value.BonusRare;
    private readonly IFurnitureDefinitionProvider _definitions = definitions;

    public async Task<BonusRareSnapshot> GetInfoAsync(PlayerId playerId, CancellationToken ct)
    {
        var definition = _config.Enabled
            ? _definitions.TryGetDefinitionByName(_config.FurnitureName)
            : null;

        if (
            playerId <= 0
            || definition is null
            || _config.CreditsRequired <= 0
            || string.IsNullOrWhiteSpace(_config.CampaignId)
            || string.IsNullOrWhiteSpace(_config.ProductCode)
        )
            return new BonusRareSnapshot
            {
                ProductCode = string.Empty,
                ProductClassId = -1,
                TotalCoinsForBonus = 0,
                CoinsStillRequiredToBuy = 0,
            };

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var configuredContext = dbCtx.ConfigureAwait(false);
        var progress = await dbCtx
            .PlayerBonusRareProgress.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId.Value && x.CampaignId == _config.CampaignId)
            .Select(x => x.CreditsTowardNextReward)
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return new BonusRareSnapshot
        {
            ProductCode = _config.ProductCode,
            ProductClassId = definition.SpriteId,
            TotalCoinsForBonus = _config.CreditsRequired,
            CoinsStillRequiredToBuy =
                _config.CreditsRequired - Math.Clamp(progress, 0, _config.CreditsRequired),
        };
    }
}
