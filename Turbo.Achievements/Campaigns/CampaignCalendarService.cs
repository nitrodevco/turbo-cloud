using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Campaigns;
using Turbo.Primitives.Campaigns;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Outgoing.Campaign;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Achievements.Campaigns;

/// <summary>
/// The campaign calendars in the database. A door is opened once: its row is written first (one
/// per player, campaign and day), then what it holds is given - the currencies and the furni under
/// a reference of the door, so a retry after a crash gives nothing twice. A gift that cannot be
/// given takes the row back, so the door can be opened again.
/// </summary>
public sealed partial class CampaignCalendarService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    IFurnitureDefinitionProvider definitions,
    ICurrencyTypeProvider currencies,
    TimeProvider time,
    ILogger<CampaignCalendarService> logger
) : ICampaignCalendarService
{
    /// <summary>The wallet and inventory references of a door: <c>calendar:&lt;campaign id&gt;:&lt;day&gt;</c>.</summary>
    public const string REFERENCE_PREFIX = "calendar:";

    public async Task<CampaignCalendarDataMessageComposer?> GetCalendarAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var now = time.GetUtcNow().UtcDateTime;
        var campaign = (await ListAsync(ct).ConfigureAwait(false))
            .Where(x => CampaignCalendarRules.IsLive(x, now))
            .OrderByDescending(x => x.StartsAt)
            .ThenByDescending(x => x.Id)
            .FirstOrDefault();

        if (campaign is null)
            return null;

        var opened = await OpenedDaysAsync(playerId, campaign.Id, ct).ConfigureAwait(false);
        var currentDay = CampaignCalendarRules.CurrentDay(campaign, now);

        return new CampaignCalendarDataMessageComposer
        {
            CampaignName = campaign.Name,
            CampaignImage = campaign.Image,
            CurrentDay = currentDay,
            CampaignDays = campaign.Days,
            OpenedDays = [.. opened.Order()],
            MissedDays = CampaignCalendarRules.MissedDays(currentDay, opened),
        };
    }

    public async Task<CampaignCalendarDoorOpenedMessageComposer> OpenDoorAsync(
        PlayerId playerId,
        string campaignName,
        int dayIndex,
        bool asStaff,
        CancellationToken ct
    )
    {
        var now = time.GetUtcNow().UtcDateTime;
        var campaign = (await ListAsync(ct).ConfigureAwait(false)).FirstOrDefault(x =>
            string.Equals(x.Name, campaignName, StringComparison.Ordinal)
        );

        if (
            campaign is null
            || !CampaignCalendarRules.IsLive(campaign, now)
            || dayIndex < 0
            || dayIndex >= campaign.Days
        )
            return CampaignCalendarDoorOpenedMessageComposer.NotOpened;

        var opened = await OpenedDaysAsync(playerId, campaign.Id, ct).ConfigureAwait(false);
        var state = CampaignCalendarRules.StateOf(
            dayIndex,
            CampaignCalendarRules.CurrentDay(campaign, now),
            opened
        );

        if (
            state == CampaignCalendarDoorState.Unlocked
            || (state != CampaignCalendarDoorState.Available && !asStaff)
        )
            return CampaignCalendarDoorOpenedMessageComposer.NotOpened;

        var reward = campaign.Rewards.FirstOrDefault(x => x.DayIndex == dayIndex);

        // A door with nothing behind it, or a product the client could not name, would open to
        // nothing on screen: the client draws an opened door only with the product's name.
        if (reward is null || reward.ShownProductCode.Length == 0)
        {
            logger.LogWarning(
                "Calendar {Campaign} has no gift for day {Day}",
                campaign.Name,
                dayIndex
            );

            return CampaignCalendarDoorOpenedMessageComposer.NotOpened;
        }

        if (!await ClaimAsync(playerId, campaign.Id, dayIndex, asStaff, ct).ConfigureAwait(false))
            return CampaignCalendarDoorOpenedMessageComposer.NotOpened;

        if (!await GiveAsync(playerId, campaign, reward, ct).ConfigureAwait(false))
        {
            await UnclaimAsync(playerId, campaign.Id, dayIndex, ct).ConfigureAwait(false);

            return CampaignCalendarDoorOpenedMessageComposer.NotOpened;
        }

        logger.LogInformation(
            "Player {PlayerId} opened day {Day} of calendar {Campaign}{Staff}",
            playerId.Value,
            dayIndex,
            campaign.Name,
            asStaff ? " (staff)" : string.Empty
        );

        return new CampaignCalendarDoorOpenedMessageComposer
        {
            DoorOpened = true,
            ProductName = reward.ShownProductCode,
            CustomImage = reward.CustomImage,
            FurnitureClassName = reward.FurnitureClassName,
        };
    }

    public async Task<ImmutableArray<CampaignCalendarDefinition>> ListAsync(CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);
        var rows = await db
            .CampaignCalendars.AsNoTracking()
            .Include(x => x.Rewards)
            .OrderBy(x => x.StartsAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows.Select(Definition)];
    }

    public async Task<CampaignCalendarDefinition?> GetAsync(int id, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);
        var row = await db
            .CampaignCalendars.AsNoTracking()
            .Include(x => x.Rewards)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        return row is null ? null : Definition(row);
    }

    public async Task<CampaignCalendarDefinition> SaveAsync(
        CampaignCalendarDefinition campaign,
        CancellationToken ct
    )
    {
        Check(campaign);

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        if (
            await db
                .CampaignCalendars.AnyAsync(x => x.Name == campaign.Name && x.Id != campaign.Id, ct)
                .ConfigureAwait(false)
        )
            throw new CampaignCalendarRefusedException(
                $"There is another calendar named {campaign.Name}.",
                "name"
            );

        CampaignCalendarEntity row;

        if (campaign.Id == 0)
        {
            row = new CampaignCalendarEntity { Name = campaign.Name, Image = campaign.Image };
            db.CampaignCalendars.Add(row);
        }
        else
        {
            row =
                await db
                    .CampaignCalendars.Include(x => x.Rewards)
                    .FirstOrDefaultAsync(x => x.Id == campaign.Id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no calendar {campaign.Id}.");
            db.CampaignCalendarRewards.RemoveRange(row.Rewards);
            row.Rewards.Clear();
        }

        row.Name = campaign.Name;
        row.Image = campaign.Image;
        row.StartsAt = DateTime.SpecifyKind(campaign.StartsAt, DateTimeKind.Utc);
        row.Days = campaign.Days;
        row.Enabled = campaign.Enabled;
        row.Rewards.AddRange(
            campaign.Rewards.Select(x => new CampaignCalendarRewardEntity
            {
                CampaignCalendarEntityId = row.Id,
                DayIndex = x.DayIndex,
                ProductCode = x.ProductCode.Trim(),
                CustomImage = x.CustomImage.Trim(),
                FurnitureClassName = x.FurnitureClassName.Trim(),
                BadgeCode = x.BadgeCode.Trim(),
                Credits = x.Credits,
                CurrencyTypeId = x.CurrencyAmount > 0 ? x.CurrencyTypeId : null,
                CurrencyAmount = x.CurrencyTypeId is null ? 0 : x.CurrencyAmount,
                EffectId = x.EffectId,
            })
        );

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return await GetAsync(row.Id, ct).ConfigureAwait(false) ?? Definition(row);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);
        var row = await db
            .CampaignCalendars.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        db.CampaignCalendars.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return true;
    }

    private void Check(CampaignCalendarDefinition campaign)
    {
        if (!NamePattern().IsMatch(campaign.Name))
            throw new CampaignCalendarRefusedException(
                "A calendar's name is 1 to 64 letters, digits, '_' or '-': it names its texts (campaign.calendar.<name>.title).",
                "name"
            );

        if (campaign.Image.Length > CampaignCalendarEntity.IMAGE_MAX_LENGTH)
            throw new CampaignCalendarRefusedException("The image is too long.", "image");

        if (campaign.Days is < 1 or > 366)
            throw new CampaignCalendarRefusedException("A calendar has 1 to 366 days.", "days");

        var days = new HashSet<int>();

        foreach (var reward in campaign.Rewards)
        {
            var day = reward.DayIndex.ToString(CultureInfo.InvariantCulture);
            var refused = (string message, string field) =>
                new CampaignCalendarRefusedException(message, field, reward.DayIndex);

            if (reward.DayIndex < 0 || reward.DayIndex >= campaign.Days)
                throw refused($"Day {day} is outside the calendar.", "dayIndex");

            if (!days.Add(reward.DayIndex))
                throw refused($"Day {day} has two gifts.", "dayIndex");

            if (reward.ShownProductCode.Length == 0)
                throw refused(
                    $"Day {day} names no product: the client shows the gift by its product data name.",
                    "productCode"
                );

            if (reward.Credits < 0)
                throw refused($"Day {day} gives a negative amount.", "credits");

            if (reward.CurrencyAmount < 0)
                throw refused($"Day {day} gives a negative amount.", "currencyAmount");

            if (
                reward.FurnitureClassName.Length > 0
                && definitions.TryGetDefinitionByName(reward.FurnitureClassName.Trim()) is null
            )
                throw refused(
                    $"Day {day}: there is no furni {reward.FurnitureClassName}.",
                    "furnitureClassName"
                );

            if (
                reward.CurrencyAmount > 0
                && (
                    reward.CurrencyTypeId is not { } typeId
                    || currencies.GetCurrencyType(typeId) is null
                )
            )
                throw refused($"Day {day}: there is no such currency.", "currencyTypeId");
        }
    }

    private async Task<HashSet<int>> OpenedDaysAsync(
        PlayerId playerId,
        int campaignId,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);
        var days = await db
            .PlayerCampaignCalendarDoors.AsNoTracking()
            .Where(x =>
                x.PlayerEntityId == playerId.Value && x.CampaignCalendarEntityId == campaignId
            )
            .Select(x => x.DayIndex)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. days];
    }

    /// <summary>Writes the door's row; false when it was there already (a second, racing open).</summary>
    private async Task<bool> ClaimAsync(
        PlayerId playerId,
        int campaignId,
        int dayIndex,
        bool asStaff,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        db.PlayerCampaignCalendarDoors.Add(
            new PlayerCampaignCalendarDoorEntity
            {
                PlayerEntityId = playerId.Value,
                CampaignCalendarEntityId = campaignId,
                DayIndex = dayIndex,
                ByStaff = asStaff,
            }
        );

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return true;
        }
        catch (DbUpdateException)
        {
            if (
                (await OpenedDaysAsync(playerId, campaignId, ct).ConfigureAwait(false)).Contains(
                    dayIndex
                )
            )
                return false;

            throw;
        }
    }

    private async Task UnclaimAsync(
        PlayerId playerId,
        int campaignId,
        int dayIndex,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);
        var row = await db
            .PlayerCampaignCalendarDoors.FirstOrDefaultAsync(
                x =>
                    x.PlayerEntityId == playerId.Value
                    && x.CampaignCalendarEntityId == campaignId
                    && x.DayIndex == dayIndex,
                ct
            )
            .ConfigureAwait(false);

        if (row is null)
            return;

        db.PlayerCampaignCalendarDoors.Remove(row);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Gives everything the door holds; false when any part could not be given.</summary>
    private async Task<bool> GiveAsync(
        PlayerId playerId,
        CampaignCalendarDefinition campaign,
        CampaignCalendarRewardDefinition reward,
        CancellationToken ct
    )
    {
        var reference = string.Create(
            CultureInfo.InvariantCulture,
            $"{REFERENCE_PREFIX}{campaign.Id}:{reward.DayIndex}"
        );
        var cause = MovementCause.New(MovementCauses.CAMPAIGN_CALENDAR, playerId.Value, reference);
        var wallet = grainFactory.GetPlayerWalletGrain(playerId);
        var given = true;

        try
        {
            if (reward.Credits > 0)
                given &=
                    await wallet
                        .CreditAsync(
                            CurrencyKind.Credits,
                            reward.Credits,
                            $"{reference}:credits",
                            cause,
                            ct
                        )
                        .ConfigureAwait(false)
                        is not WalletCreditResult.Rejected;

            if (
                reward.CurrencyAmount > 0
                && reward.CurrencyTypeId is { } typeId
                && currencies.GetCurrencyType(typeId) is { } currency
            )
                given &=
                    await wallet
                        .CreditAsync(
                            new CurrencyKind
                            {
                                CurrencyType = currency.CurrencyType,
                                ActivityPointType = currency.ActivityPointType,
                            },
                            reward.CurrencyAmount,
                            $"{reference}:currency",
                            cause.StartingAt(1),
                            ct
                        )
                        .ConfigureAwait(false)
                        is not WalletCreditResult.Rejected;

            // A badge the player has already is theirs either way.
            if (reward.BadgeCode.Length > 0)
                await grainFactory
                    .GetPlayerBadgeGrain(playerId)
                    .GiveBadgeAsync(reward.BadgeCode, ct)
                    .ConfigureAwait(false);

            if (
                reward.FurnitureClassName.Length > 0
                && definitions.TryGetDefinitionByName(reward.FurnitureClassName) is { } definition
            )
                // False when this door's furni was given already, which is given all the same.
                await grainFactory
                    .GetInventoryGrain(playerId)
                    .GrantFurnitureOnceAsync(definition.Id, reference, cause.StartingAt(2), ct)
                    .ConfigureAwait(false);

            if (reward.EffectId is { } effectId)
                given &=
                    await grainFactory
                        .GetPlayerEffectGrain(playerId)
                        .GiveEffectAsync(effectId, 0, 1, false, ct)
                        .ConfigureAwait(false)
                        is EffectGrantResult.Granted
                            or EffectGrantResult.AlreadyPermanent;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Giving day {Day} of calendar {Campaign} to player {PlayerId} failed",
                reward.DayIndex,
                campaign.Name,
                playerId.Value
            );

            return false;
        }

        if (!given)
            logger.LogError(
                "Day {Day} of calendar {Campaign} could not be given to player {PlayerId}",
                reward.DayIndex,
                campaign.Name,
                playerId.Value
            );

        return given;
    }

    private static CampaignCalendarDefinition Definition(CampaignCalendarEntity row) =>
        new()
        {
            Id = row.Id,
            Name = row.Name,
            Image = row.Image,
            StartsAt = DateTime.SpecifyKind(row.StartsAt, DateTimeKind.Utc),
            Days = row.Days,
            Enabled = row.Enabled,
            Rewards =
            [
                .. row
                    .Rewards.OrderBy(x => x.DayIndex)
                    .Select(x => new CampaignCalendarRewardDefinition
                    {
                        DayIndex = x.DayIndex,
                        ProductCode = x.ProductCode,
                        CustomImage = x.CustomImage,
                        FurnitureClassName = x.FurnitureClassName,
                        BadgeCode = x.BadgeCode,
                        Credits = x.Credits,
                        CurrencyTypeId = x.CurrencyTypeId,
                        CurrencyAmount = x.CurrencyAmount,
                        EffectId = x.EffectId,
                    }),
            ],
        };

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex NamePattern();
}
