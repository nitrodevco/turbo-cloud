using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Reception;

/// <summary>
/// The bonus rare (<see cref="IBonusRareService"/>): campaigns in <c>bonus_rare_campaigns</c>,
/// each player's progress in <c>player_bonus_rare_progress</c>, bought credits' receipts in
/// <c>bonus_rare_receipts</c>. Progress is added in the database, and a reward is taken off it
/// there (<c>credits = credits - target where credits &gt;= target</c>) before the furniture is
/// given, so each target reached gives one reward, whichever silo counts it. The campaigns are
/// kept for <see cref="ReceptionConfig.CacheSeconds"/>.
/// </summary>
public sealed class BonusRareService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<CatalogConfig> config,
    IFurnitureDefinitionProvider definitions,
    IGrainFactory grainFactory,
    TimeProvider time,
    ILogger<BonusRareService> logger
) : IBonusRareService
{
    private static readonly BonusRareSnapshot HIDDEN = new()
    {
        ProductCode = string.Empty,
        ProductClassId = -1,
        TotalCoinsForBonus = 0,
        CoinsStillRequiredToBuy = 0,
    };

    private readonly ReceptionConfig _config = config.Value.Reception;

    private (
        DateTimeOffset ReadAt,
        ImmutableArray<BonusRareCampaignSnapshot> Campaigns
    )? _campaigns;

    public async Task<BonusRareSnapshot> GetInfoAsync(PlayerId player, CancellationToken ct)
    {
        var campaign = await RunningAsync(ct).ConfigureAwait(false);

        return campaign is null || player <= 0
            ? HIDDEN
            : await InfoAsync(player, campaign, ct).ConfigureAwait(false);
    }

    public async Task RecordCatalogSpendingAsync(PlayerId player, int credits, CancellationToken ct)
    {
        if (credits <= 0)
            return;

        var campaign = await RunningAsync(ct).ConfigureAwait(false);

        if (campaign is not { Source: BonusRareSource.CatalogSpending })
            return;

        await CountAsync(player, campaign, credits, ct).ConfigureAwait(false);
    }

    public async Task<BonusRarePurchaseResult> RecordPurchaseAsync(
        PlayerId player,
        int credits,
        string reference,
        CancellationToken ct
    )
    {
        reference = reference.Trim();

        if (
            credits <= 0
            || player <= 0
            || reference.Length is 0 or > BonusRareReceiptEntity.REFERENCE_MAX_LENGTH
        )
            return BonusRarePurchaseResult.Rejected;

        var campaign = await RunningAsync(ct).ConfigureAwait(false);

        if (campaign is not { Source: BonusRareSource.PurchasedCredits })
            return BonusRarePurchaseResult.NoCampaign;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (!await dbCtx.Players.AnyAsync(x => x.Id == player.Value, ct).ConfigureAwait(false))
            return BonusRarePurchaseResult.Rejected;

        // The receipt first: its unique reference is what makes a purchase count once.
        dbCtx.BonusRareReceipts.Add(
            new BonusRareReceiptEntity
            {
                Reference = reference,
                PlayerEntityId = player.Value,
                CampaignCode = campaign.Code,
                Credits = credits,
            }
        );

        try
        {
            await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            if (await ReceiptExistsAsync(reference, ct).ConfigureAwait(false))
                return BonusRarePurchaseResult.AlreadyRecorded;

            throw;
        }

        await CountAsync(player, campaign, credits, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Recorded {Credits} bought credits for player {PlayerId}'s bonus rare ({Reference})",
            credits,
            player.Value,
            reference
        );

        return BonusRarePurchaseResult.Recorded;
    }

    public Task<ImmutableArray<BonusRareCampaignSnapshot>> ListAsync(CancellationToken ct) =>
        ReadCampaignsAsync(ct);

    public async Task<BonusRareCampaignSnapshot> SaveAsync(
        BonusRareCampaignSnapshot campaign,
        CancellationToken ct
    )
    {
        var code = campaign.Code.Trim();
        var furniture = campaign.FurnitureName.Trim();
        var product = campaign.ProductCode.Trim();

        if (code.Length is 0 or > BonusRareCampaignEntity.CODE_MAX_LENGTH)
            throw new ArgumentException(
                $"A campaign needs a code, {BonusRareCampaignEntity.CODE_MAX_LENGTH} characters at most.",
                nameof(campaign)
            );

        if (definitions.TryGetDefinitionByName(furniture) is null)
            throw new ArgumentException(
                $"The hotel has no furniture {furniture}.",
                nameof(campaign)
            );

        if (product.Length > BonusRareCampaignEntity.NAME_MAX_LENGTH)
            throw new ArgumentException("The product code is too long.", nameof(campaign));

        if (campaign.CreditsRequired <= 0)
            throw new ArgumentException("A reward takes at least one credit.", nameof(campaign));

        if (!Enum.IsDefined(campaign.Source))
            throw new ArgumentException("That is nothing a campaign can count.", nameof(campaign));

        if (campaign.EndsAt is { } ends && ends <= campaign.StartsAt)
            throw new ArgumentException("A campaign ends after it starts.", nameof(campaign));

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        if (
            await dbCtx
                .BonusRareCampaigns.AnyAsync(x => x.Code == code && x.Id != campaign.Id, ct)
                .ConfigureAwait(false)
        )
            throw new ArgumentException($"There is a campaign {code} already.", nameof(campaign));

        BonusRareCampaignEntity row;

        if (campaign.Id == 0)
        {
            row = new BonusRareCampaignEntity
            {
                Code = code,
                FurnitureName = furniture,
                ProductCode = product,
            };
            dbCtx.BonusRareCampaigns.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .BonusRareCampaigns.FirstOrDefaultAsync(x => x.Id == campaign.Id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException(
                    $"There is no bonus rare campaign {campaign.Id}.",
                    nameof(campaign)
                );
        }

        row.Code = code;
        row.FurnitureName = furniture;
        // The widget names the reward by its product; the furniture's own name does when none is given.
        row.ProductCode = product.Length > 0 ? product : furniture;
        row.CreditsRequired = campaign.CreditsRequired;
        row.Source = campaign.Source;
        row.StartsAt = DateTime.SpecifyKind(campaign.StartsAt, DateTimeKind.Utc);
        row.EndsAt = campaign.EndsAt is { } end
            ? DateTime.SpecifyKind(end, DateTimeKind.Utc)
            : null;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        _campaigns = null;
        logger.LogInformation("Bonus rare campaign {Code} saved", row.Code);

        return row.ToSnapshot();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .BonusRareCampaigns.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        dbCtx.BonusRareCampaigns.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        _campaigns = null;
        logger.LogInformation("Bonus rare campaign {Code} removed", row.Code);

        return true;
    }

    public async Task<BonusRareStandingSnapshot> GetStandingAsync(string code, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = dbCtx.PlayerBonusRareProgress.AsNoTracking().Where(x => x.CampaignId == code);

        return new BonusRareStandingSnapshot
        {
            Code = code,
            PlayersInProgress = await rows.CountAsync(x => x.CreditsTowardNextReward > 0, ct)
                .ConfigureAwait(false),
            RewardsGiven = await rows.SumAsync(x => x.RewardsReceived, ct).ConfigureAwait(false),
        };
    }

    /// <summary>
    /// Adds the credits to the player's progress, gives a reward for each target reached, and
    /// tells the player's widget where they stand now.
    /// </summary>
    private async Task CountAsync(
        PlayerId player,
        BonusRareCampaignSnapshot campaign,
        int credits,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var progress = dbCtx.PlayerBonusRareProgress.Where(x =>
            x.PlayerEntityId == player.Value && x.CampaignId == campaign.Code
        );

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var added = await progress
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(
                            x => x.CreditsTowardNextReward,
                            x => x.CreditsTowardNextReward + credits
                        ),
                    ct
                )
                .ConfigureAwait(false);

            if (added > 0)
                break;

            var row = new PlayerBonusRareProgressEntity
            {
                PlayerEntityId = player.Value,
                CampaignId = campaign.Code,
                CreditsTowardNextReward = credits,
            };

            dbCtx.PlayerBonusRareProgress.Add(row);

            try
            {
                await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

                break;
            }
            catch (DbUpdateException ex)
            {
                // Another silo made the row first: add to it.
                dbCtx.Entry(row).State = EntityState.Detached;
                logger.LogDebug(ex, "Bonus rare progress was made at the same time; adding to it");
            }
        }

        while (true)
        {
            var taken = await progress
                .Where(x => x.CreditsTowardNextReward >= campaign.CreditsRequired)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(
                                x => x.CreditsTowardNextReward,
                                x => x.CreditsTowardNextReward - campaign.CreditsRequired
                            )
                            .SetProperty(x => x.RewardsReceived, x => x.RewardsReceived + 1),
                    ct
                )
                .ConfigureAwait(false);

            if (taken == 0)
                break;

            if (await GiveAsync(player, campaign, ct).ConfigureAwait(false))
                continue;

            // Not given: the credits go back, to be given when it can be.
            await progress
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(
                                x => x.CreditsTowardNextReward,
                                x => x.CreditsTowardNextReward + campaign.CreditsRequired
                            )
                            .SetProperty(x => x.RewardsReceived, x => x.RewardsReceived - 1),
                    ct
                )
                .ConfigureAwait(false);

            break;
        }

        await grainFactory
            .SendComposerToPlayerAsync(
                player,
                Composer(await InfoAsync(player, campaign, ct).ConfigureAwait(false)),
                ct
            )
            .ConfigureAwait(false);
    }

    private async Task<bool> GiveAsync(
        PlayerId player,
        BonusRareCampaignSnapshot campaign,
        CancellationToken ct
    )
    {
        var definition = definitions.TryGetDefinitionByName(campaign.FurnitureName);

        try
        {
            if (
                definition is not null
                && await grainFactory
                    .GetInventoryGrain(player)
                    .GrantFurnitureAsync(definition.Id, null, ct)
                    .ConfigureAwait(false)
                    is not null
            )
            {
                logger.LogInformation(
                    "Player {PlayerId} was given the bonus rare {Furniture} ({Code})",
                    player.Value,
                    campaign.FurnitureName,
                    campaign.Code
                );

                return true;
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Giving player {PlayerId} the bonus rare {Furniture} failed",
                player.Value,
                campaign.FurnitureName
            );

            return false;
        }

        logger.LogError(
            "The bonus rare {Furniture} of campaign {Code} has no definition to give",
            campaign.FurnitureName,
            campaign.Code
        );

        return false;
    }

    private async Task<BonusRareSnapshot> InfoAsync(
        PlayerId player,
        BonusRareCampaignSnapshot campaign,
        CancellationToken ct
    )
    {
        var definition = definitions.TryGetDefinitionByName(campaign.FurnitureName);

        if (definition is null)
            return HIDDEN;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var progress = await dbCtx
            .PlayerBonusRareProgress.AsNoTracking()
            .Where(x => x.PlayerEntityId == player.Value && x.CampaignId == campaign.Code)
            .Select(x => x.CreditsTowardNextReward)
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return new BonusRareSnapshot
        {
            ProductCode = campaign.ProductCode,
            ProductClassId = definition.SpriteId,
            TotalCoinsForBonus = campaign.CreditsRequired,
            CoinsStillRequiredToBuy =
                campaign.CreditsRequired - Math.Clamp(progress, 0, campaign.CreditsRequired),
        };
    }

    private static BonusRareInfoMessageComposer Composer(BonusRareSnapshot info) =>
        new()
        {
            ProductType = info.ProductCode,
            ProductClassId = info.ProductClassId,
            TotalCoinsForBonus = info.TotalCoinsForBonus,
            CoinsStillRequiredToBuy = info.CoinsStillRequiredToBuy,
        };

    private async Task<bool> ReceiptExistsAsync(string reference, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .BonusRareReceipts.AnyAsync(x => x.Reference == reference, ct)
            .ConfigureAwait(false);
    }

    /// <summary>The campaign running now: the last one started and not ended.</summary>
    private async Task<BonusRareCampaignSnapshot?> RunningAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var read = _campaigns;

        if (read is null || now - read.Value.ReadAt >= TimeSpan.FromSeconds(_config.CacheSeconds))
        {
            read = (now, await ReadCampaignsAsync(ct).ConfigureAwait(false));
            _campaigns = read;
        }

        return read.Value.Campaigns.Where(x => x.IsRunning(now.UtcDateTime)).MaxBy(x => x.StartsAt);
    }

    private async Task<ImmutableArray<BonusRareCampaignSnapshot>> ReadCampaignsAsync(
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .BonusRareCampaigns.AsNoTracking()
            .OrderByDescending(x => x.StartsAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows.Select(x => x.ToSnapshot())];
    }
}
