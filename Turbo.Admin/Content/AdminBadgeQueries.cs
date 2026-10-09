using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Badges;
using Turbo.Primitives.Badges.Enums;

namespace Turbo.Admin.Content;

/// <summary>
/// The hotel's badges as staff look them up: every code a player holds or the hotel pinned a
/// rarity for, with how many hold it, and who. A badge needs no row of its own; a row in
/// <c>badge_definitions</c> only pins its rarity.
/// </summary>
public sealed class AdminBadgeQueries(IDbContextFactory<TurboDbContext> dbCtxFactory)
{
    /// <summary>Badges found at most, and holders listed at most.</summary>
    public const int LIMIT = 100;

    public async Task<List<BadgeItem>> SearchAsync(string? query, CancellationToken ct)
    {
        var words = (query ?? string.Empty).Trim();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var held = dbCtx.PlayerBadges.AsNoTracking();
        var pinned = dbCtx.BadgeDefinitions.AsNoTracking();

        if (words.Length > 0)
        {
            held = held.Where(x => x.BadgeCode.Contains(words));
            pinned = pinned.Where(x => x.BadgeCode.Contains(words));
        }

        var counts = await held.GroupBy(x => x.BadgeCode)
            .Select(x => new { Code = x.Key, Holders = x.Count() })
            .OrderByDescending(x => x.Holders)
            .ThenBy(x => x.Code)
            .Take(LIMIT)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var rarities = await pinned
            .OrderBy(x => x.BadgeCode)
            .Take(LIMIT)
            .ToDictionaryAsync(x => x.BadgeCode, x => x.Rarity, StringComparer.Ordinal, ct)
            .ConfigureAwait(false);
        var codes = counts.Select(x => x.Code).Union(rarities.Keys, StringComparer.Ordinal);
        var holders = counts.ToDictionary(x => x.Code, x => x.Holders, StringComparer.Ordinal);

        // A pinned code no one holds isn't in the counts: look its holders up too.
        var missing = rarities.Keys.Where(x => !holders.ContainsKey(x)).ToList();

        if (missing.Count > 0)
            foreach (
                var row in await dbCtx
                    .PlayerBadges.AsNoTracking()
                    .Where(x => missing.Contains(x.BadgeCode))
                    .GroupBy(x => x.BadgeCode)
                    .Select(x => new { Code = x.Key, Holders = x.Count() })
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
            )
                holders[row.Code] = row.Holders;

        return
        [
            .. codes
                .Select(code => new BadgeItem(
                    code,
                    holders.GetValueOrDefault(code),
                    rarities.GetValueOrDefault(code)
                ))
                .OrderByDescending(x => x.Holders)
                .ThenBy(x => x.Code, StringComparer.Ordinal)
                .Take(LIMIT),
        ];
    }

    public async Task<List<BadgeHolderItem>> HoldersAsync(string code, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .PlayerBadges.AsNoTracking()
            .Where(x => x.BadgeCode == code)
            .OrderBy(x => x.Id)
            .Take(LIMIT)
            .Select(x => new BadgeHolderItem(x.PlayerEntityId, x.PlayerEntity.Name, x.SlotId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>Pins a badge's rarity, or with none lets it be counted from how many hold it.</summary>
    public async Task SetRarityAsync(string code, BadgeRarityType? rarity, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .BadgeDefinitions.FirstOrDefaultAsync(x => x.BadgeCode == code, ct)
            .ConfigureAwait(false);

        if (rarity is null)
        {
            if (row is not null)
                dbCtx.BadgeDefinitions.Remove(row);
        }
        else if (row is null)
        {
            dbCtx.BadgeDefinitions.Add(
                new BadgeDefinitionEntity { BadgeCode = code, Rarity = rarity }
            );
        }
        else
        {
            row.Rarity = rarity;
        }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
