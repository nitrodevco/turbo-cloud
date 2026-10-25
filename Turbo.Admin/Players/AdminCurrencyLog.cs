using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Admin.Players;

/// <summary>
/// The currency log as staff read it: every change to a wallet the server's ledger kept
/// (<c>currency_ledger</c>), newest first, narrowed to a player, a currency, or a cause (its
/// code, or the start of one: <c>market</c> takes in every marketplace cause). With a player, what
/// came in and went out of each currency across the whole filter. Counted and sorted as the chat
/// log is. The ledger only grows while movements are recorded (<c>Turbo:Movements:Enabled</c>),
/// which the answer says. Read-only.
/// </summary>
public sealed class AdminCurrencyLog(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config,
    IOptionsMonitor<MovementsConfig> movements
)
{
    private const string ESCAPE = "\\";

    /// <summary>
    /// The columns the log sorts by: when, the change's id, which its key orders already. An
    /// amount would sort the whole ledger for each page.
    /// </summary>
    private static readonly ListSorts<CurrencyLedgerEntryEntity> SORTS =
        new ListSorts<CurrencyLedgerEntryEntity>().Add("when", ListOrder.DESC, x => x.Id);

    /// <summary>A page of the log, counted up to <see cref="LogCounts.LIMIT"/>.</summary>
    public async Task<CurrencyLogResponse> SearchAsync(
        string? player,
        int? currency,
        string? cause,
        int page,
        string? sort,
        string? dir,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.CurrencyLogPageSize);
        var order = SORTS.Resolve(sort, dir);
        var recording = movements.CurrentValue is { Enabled: true, CurrencyLedger: true };
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var types = await db
            .CurrencyTypes.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.CurrencyType,
                x.ActivityPointType,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        CurrencyLogCurrency[] currencies =
        [
            .. types.Select(x => new CurrencyLogCurrency(
                x.Id,
                string.IsNullOrWhiteSpace(x.Name) ? x.CurrencyType.ToString() : x.Name
            )),
        ];

        string NameOf(CurrencyType type, int? points) =>
            types.FirstOrDefault(x => x.CurrencyType == type && x.ActivityPointType == points)
                is { } known
                ? currencies.First(x => x.TypeId == known.Id).Name
            : points is { } pointType ? $"{type} {pointType}"
            : type.ToString();

        var nobody = new CurrencyLogResponse(
            0,
            false,
            1,
            pageSize,
            order,
            recording,
            currencies,
            [],
            null
        );
        var entries = db.CurrencyLedger.AsNoTracking();
        var who = (player ?? string.Empty).Trim();

        if (who.Length > 0)
        {
            if (await PlayerIdAsync(db, who, ct).ConfigureAwait(false) is not { } found)
                return nobody;

            entries = entries.Where(x => x.PlayerId == found);
        }

        if (currency is { } typeId)
        {
            if (types.FirstOrDefault(x => x.Id == typeId) is not { } type)
                return nobody;

            entries = entries.Where(x =>
                x.CurrencyType == type.CurrencyType && x.ActivityPointType == type.ActivityPointType
            );
        }

        var code = (cause ?? string.Empty).Trim();

        if (code.Length > 0)
        {
            var like = code.Replace(ESCAPE, ESCAPE + ESCAPE, StringComparison.Ordinal)
                .Replace("%", ESCAPE + "%", StringComparison.Ordinal)
                .Replace("_", ESCAPE + "_", StringComparison.Ordinal);

            entries = entries.Where(x => EF.Functions.Like(x.Cause, like + "%", ESCAPE));
        }

        var sorted = SORTS.Apply(entries, order);
        var (total, capped) = await LogCounts.CountAsync(sorted, ct).ConfigureAwait(false);

        page = LogCounts.Page(page, total, pageSize);

        var rows = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // A player's in and out per currency, across everything the filter takes in.
        CurrencyLogTotal[]? totals = null;

        if (who.Length > 0)
        {
            var sums = await entries
                .GroupBy(x => new { x.CurrencyType, x.ActivityPointType })
                .Select(x => new
                {
                    x.Key.CurrencyType,
                    x.Key.ActivityPointType,
                    In = x.Sum(y => y.Amount > 0 ? y.Amount : 0),
                    Out = x.Sum(y => y.Amount < 0 ? -y.Amount : 0),
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            totals =
            [
                .. sums.OrderBy(x => x.CurrencyType)
                    .ThenBy(x => x.ActivityPointType)
                    .Select(x => new CurrencyLogTotal(
                        NameOf(x.CurrencyType, x.ActivityPointType),
                        x.In,
                        x.Out
                    )),
            ];
        }

        var ids = rows.SelectMany(x => new[] { x.PlayerId, x.ActorPlayerId, x.OtherOwnerId })
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var names = await db
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);

        return new CurrencyLogResponse(
            total,
            capped,
            page,
            pageSize,
            order,
            recording,
            currencies,
            [
                .. rows.Select(x => new CurrencyLogEntry(
                    x.Id,
                    x.OccurredAt,
                    x.PlayerId,
                    names.GetValueOrDefault(x.PlayerId),
                    NameOf(x.CurrencyType, x.ActivityPointType),
                    x.Amount,
                    x.Cause,
                    x.CauseRef,
                    x.ActorPlayerId == 0 ? null : x.ActorPlayerId,
                    x.ActorPlayerId == 0 ? null : names.GetValueOrDefault(x.ActorPlayerId),
                    Place(x.OtherKind),
                    x.OtherOwnerId == 0 ? null : x.OtherOwnerId,
                    x.OtherOwnerId == 0 ? null : names.GetValueOrDefault(x.OtherOwnerId),
                    x.OtherPlaceId == 0 ? null : x.OtherPlaceId
                )),
            ],
            totals
        );
    }

    /// <summary>The other side of a move, as the panel names it.</summary>
    private static string Place(PlaceKind kind) =>
        kind switch
        {
            PlaceKind.Wallet => "wallet",
            PlaceKind.MarketplaceOffer => "offer",
            PlaceKind.ChestCredits => "chest",
            PlaceKind.Inventory => "inventory",
            PlaceKind.Room => "room",
            PlaceKind.Holder => "holder",
            _ => "none",
        };

    /// <summary>A player by id, or by name.</summary>
    private static async Task<int?> PlayerIdAsync(
        TurboDbContext db,
        string who,
        CancellationToken ct
    ) =>
        int.TryParse(who, out var id)
            ? id
            : await db
                .Players.AsNoTracking()
                .Where(x => x.NameKey == PlayerNames.Key(who))
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
}
