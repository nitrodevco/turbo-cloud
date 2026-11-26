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
using Turbo.Database.Context;
using Turbo.Database.Entities.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Players;

/// <summary>
/// Every ban and caution the hotel has given, from <c>player_sanctions</c>: the sanctions log,
/// newest first, narrowed to one player, one kind, or those that hold now. A silence and a trade
/// lock are not sanctions (they are permission denials, in the permission log). Read-only; its
/// pages are counted as the other logs' are (<see cref="LogCounts"/>).
/// </summary>
public sealed class AdminSanctions(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config,
    TimeProvider timeProvider
)
{
    /// <summary>The one column the log sorts by: when (the sanction's id), newest first.</summary>
    private static readonly ListSorts<PlayerSanctionEntity> SORTS =
        new ListSorts<PlayerSanctionEntity>().Add("when", ListOrder.DESC, x => x.Id);

    /// <summary>
    /// A page of the log, narrowed to a player (by name or id), a kind (<c>ban</c> or
    /// <c>caution</c>; any other reads as every kind) and, with <paramref name="active"/>, to those
    /// that hold now (or, false, those that no longer do), counted up to <see cref="LogCounts.LIMIT"/>.
    /// </summary>
    public async Task<SanctionLogResponse> SearchAsync(
        string? player,
        string? kind,
        bool? active,
        int page,
        string? sort,
        string? dir,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.SanctionsPageSize);
        var order = SORTS.Resolve(sort, dir);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var sanctions = db.PlayerSanctions.AsNoTracking();
        var who = (player ?? string.Empty).Trim();

        if (who.Length > 0)
        {
            int? playerId = int.TryParse(who, out var id)
                ? id
                : await db
                    .Players.AsNoTracking()
                    .Where(x => x.NameKey == PlayerNames.Key(who))
                    .Select(x => (int?)x.Id)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

            if (playerId is not { } found)
                return new SanctionLogResponse(0, false, 1, pageSize, order, []);

            sanctions = sanctions.Where(x => x.PlayerEntityId == found);
        }

        if (Enum.TryParse<SanctionKind>(kind, ignoreCase: true, out var only))
            sanctions = sanctions.Where(x => x.Kind == only);

        if (active is { } holds)
            sanctions = holds
                ? sanctions.Where(x =>
                    x.RevokedAt == null && (x.ExpiresAt == null || x.ExpiresAt > now)
                )
                : sanctions.Where(x =>
                    x.RevokedAt != null || (x.ExpiresAt != null && x.ExpiresAt <= now)
                );

        var sorted = SORTS.Apply(sanctions, order);
        var (total, capped) = await LogCounts.CountAsync(sorted, ct).ConfigureAwait(false);

        page = LogCounts.Page(page, total, pageSize);

        var rows = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // The players and staff the page names, in one lookup: the ids are plain columns, so a name is
        // null for one who is gone.
        var ids = rows.SelectMany(x =>
                new[] { x.PlayerEntityId, x.IssuerEntityId, x.RevokedByEntityId }
            )
            .OfType<int>()
            .Distinct()
            .ToList();
        var names = await db
            .Players.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);

        return new SanctionLogResponse(
            total,
            capped,
            page,
            pageSize,
            order,
            [
                .. rows.Select(x => new SanctionLogEntry(
                    x.Id,
                    x.CreatedAt,
                    x.PlayerEntityId,
                    NameOf(names, x.PlayerEntityId),
                    x.Kind.ToString(),
                    x.Reason,
                    x.IssuerEntityId,
                    NameOf(names, x.IssuerEntityId),
                    x.ExpiresAt,
                    x.RevokedAt,
                    x.RevokedByEntityId,
                    NameOf(names, x.RevokedByEntityId),
                    x.RevokedAt is null && (x.ExpiresAt is null || x.ExpiresAt > now)
                )),
            ]
        );
    }

    private static string? NameOf(Dictionary<int, string> names, int? id) =>
        id is { } known && names.TryGetValue(known, out var name) ? name : null;
}
