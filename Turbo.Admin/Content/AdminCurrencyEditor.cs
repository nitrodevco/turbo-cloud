using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Admin.Content;

/// <summary>
/// The hotel's currency types as staff manage them: credits, and each kind of activity points
/// (duckets, diamonds, a seasonal currency) the client shows by its number. The server keeps them
/// in <see cref="ICurrencyTypeProvider"/>, which each change reloads, so wallets, vouchers and
/// <c>:give</c> use it at once. What a currency is (its type and activity point number) is what
/// players' balances, the catalog's prices and vouchers are kept in, so it can't change while any
/// of those use it, and a currency in use is turned off rather than deleted.
/// </summary>
public sealed partial class AdminCurrencyEditor(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ICurrencyTypeProvider currencies,
    ILogger<AdminCurrencyEditor> logger
)
{
    public const int NAME_MAX_LENGTH = 32;

    /// <summary>The highest activity point number the client is sent; it reads it as an int.</summary>
    public const int ACTIVITY_POINT_TYPE_MAX = 1000;

    public async Task<CurrencyListResponse> ListAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx
            .CurrencyTypes.AsNoTracking()
            .OrderBy(x => x.CurrencyType)
            .ThenBy(x => x.ActivityPointType)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var holders = await dbCtx
            .PlayerCurrencies.AsNoTracking()
            .GroupBy(x => x.CurrencyTypeEntityId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct)
            .ConfigureAwait(false);
        var offers = await dbCtx
            .CatalogOffers.AsNoTracking()
            .Where(x => x.CurrencyTypeId != null)
            .GroupBy(x => x.CurrencyTypeId!.Value)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct)
            .ConfigureAwait(false);
        var vouchers = await dbCtx
            .Vouchers.AsNoTracking()
            .Where(x => x.CurrencyTypeEntityId != null)
            .GroupBy(x => x.CurrencyTypeEntityId!.Value)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct)
            .ConfigureAwait(false);

        return new CurrencyListResponse([
            .. rows.Select(x => new CurrencyItem(
                x.Id,
                x.Name ?? string.Empty,
                NameOf(x.CurrencyType),
                x.ActivityPointType,
                x.Enabled,
                holders.GetValueOrDefault(x.Id),
                offers.GetValueOrDefault(x.Id),
                vouchers.GetValueOrDefault(x.Id)
            )),
        ]);
    }

    /// <summary>
    /// Adds a currency (row id 0) or changes one; its id. Refused, with why, for a name that can't
    /// be typed after <c>:give</c> or is taken, a kind another currency is, a change of kind while
    /// it is in use, or turning credits off.
    /// </summary>
    public async Task<int> SaveAsync(
        int id,
        CurrencyRequest request,
        string actor,
        CancellationToken ct
    )
    {
        var name = (request.Name ?? string.Empty).Trim().ToLowerInvariant();

        if (!NamePattern().IsMatch(name))
            throw new ArgumentException(
                $"A currency's name is how :give names it: lowercase letters, digits and _, {NAME_MAX_LENGTH} at most."
            );

        if (TypeOf(request.Type) is not { } type)
            throw new ArgumentException(
                "A currency is credits, silver, emeralds or activity points."
            );

        int? activityPointType =
            type == CurrencyType.ActivityPoints ? request.ActivityPointType : null;

        if (
            type == CurrencyType.ActivityPoints
            && activityPointType is not (>= 0 and <= ACTIVITY_POINT_TYPE_MAX)
        )
            throw new ArgumentException(
                $"Activity points need the number the client shows them by, 0 to {ACTIVITY_POINT_TYPE_MAX} (0 is duckets, 5 diamonds)."
            );

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var others = await dbCtx
            .CurrencyTypes.AsNoTracking()
            .Where(x => x.Id != id)
            .Select(x => new
            {
                x.Name,
                x.CurrencyType,
                x.ActivityPointType,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (others.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"There is a currency called {name} already.");

        if (others.Any(x => x.CurrencyType == type && x.ActivityPointType == activityPointType))
            throw new ArgumentException(
                activityPointType is { } number
                    ? $"Activity points {number} are a currency already."
                    : $"{NameOf(type)} is a currency already."
            );

        CurrencyTypeEntity row;

        if (id == 0)
        {
            row = new CurrencyTypeEntity
            {
                Name = name,
                CurrencyType = type,
                ActivityPointType = activityPointType,
                Enabled = request.Enabled ?? true,
            };
            dbCtx.CurrencyTypes.Add(row);
        }
        else
        {
            row =
                await dbCtx
                    .CurrencyTypes.FirstOrDefaultAsync(x => x.Id == id, ct)
                    .ConfigureAwait(false)
                ?? throw new ArgumentException($"There is no currency {id}.");

            if (
                (row.CurrencyType != type || row.ActivityPointType != activityPointType)
                && await InUseAsync(dbCtx, id, ct).ConfigureAwait(false) is { } use
            )
                throw new ArgumentException(
                    $"{use}, kept in what it is now; it can't become another kind. Add a new currency instead."
                );

            row.Name = name;
            row.CurrencyType = type;
            row.ActivityPointType = activityPointType;
            row.Enabled = request.Enabled ?? row.Enabled;
        }

        if (row.CurrencyType == CurrencyType.Credits && !row.Enabled)
            throw new ArgumentException("Credits can't be turned off; every wallet has them.");

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await currencies.ReloadAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "{Actor} saved the currency {CurrencyId} ({Name}: {Type} {ActivityPointType}, enabled {Enabled})",
            actor,
            row.Id,
            row.Name,
            row.CurrencyType,
            row.ActivityPointType,
            row.Enabled
        );

        return row.Id;
    }

    /// <summary>
    /// Deletes a currency nothing uses; false when there is none. Refused, with why, while
    /// players hold it, offers are priced in it or vouchers give it: turn it off instead.
    /// </summary>
    public async Task<bool> DeleteAsync(int id, string actor, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .CurrencyTypes.FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        if (row.CurrencyType == CurrencyType.Credits)
            throw new ArgumentException("Credits can't be deleted; every wallet has them.");

        if (await InUseAsync(dbCtx, id, ct).ConfigureAwait(false) is { } use)
            throw new ArgumentException($"{use}; turn it off instead.");

        dbCtx.CurrencyTypes.Remove(row);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        await currencies.ReloadAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "{Actor} deleted the currency {CurrencyId} ({Name})",
            actor,
            id,
            row.Name
        );

        return true;
    }

    /// <summary>What keeps a currency in use, in words; null when nothing does.</summary>
    private static async Task<string?> InUseAsync(
        TurboDbContext dbCtx,
        int id,
        CancellationToken ct
    )
    {
        var holders = await dbCtx
            .PlayerCurrencies.CountAsync(x => x.CurrencyTypeEntityId == id, ct)
            .ConfigureAwait(false);

        if (holders > 0)
            return $"{holders} {(holders == 1 ? "player holds" : "players hold")} it";

        var offers = await dbCtx
            .CatalogOffers.CountAsync(x => x.CurrencyTypeId == id, ct)
            .ConfigureAwait(false);

        if (offers > 0)
            return $"{offers} catalog {(offers == 1 ? "offer is" : "offers are")} priced in it";

        var vouchers = await dbCtx
            .Vouchers.CountAsync(x => x.CurrencyTypeEntityId == id, ct)
            .ConfigureAwait(false);

        return vouchers > 0
            ? $"{vouchers} {(vouchers == 1 ? "voucher gives" : "vouchers give")} it"
            : null;
    }

    /// <summary>A currency's type as the panel names it.</summary>
    public static string NameOf(CurrencyType type) =>
        type switch
        {
            CurrencyType.Credits => "credits",
            CurrencyType.Silver => "silver",
            CurrencyType.Emeralds => "emeralds",
            CurrencyType.ActivityPoints => "activity_points",
            _ => type.ToString().ToLowerInvariant(),
        };

    private static CurrencyType? TypeOf(string? name) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "credits" => CurrencyType.Credits,
            "silver" => CurrencyType.Silver,
            "emeralds" => CurrencyType.Emeralds,
            "activity_points" => CurrencyType.ActivityPoints,
            _ => null,
        };

    [GeneratedRegex("^[a-z0-9_]{1,32}$")]
    private static partial Regex NamePattern();
}
