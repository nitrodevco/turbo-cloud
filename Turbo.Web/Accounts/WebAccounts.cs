using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Security;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Web.Discord;

namespace Turbo.Web.Accounts;

/// <summary>
/// Players as the public site knows them: by the Discord account linked to them, and made from
/// one. A new player is the hotel's own (<see cref="IPlayerAccountService"/>), with the link
/// written beside it.
/// </summary>
public sealed class WebAccounts(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IPlayerAccountService accounts,
    IOwnerBootstrap owner,
    IWordFilter wordFilter,
    ILogger<WebAccounts> logger
)
{
    /// <summary>The player the Discord account is linked to, its username kept current; null when none.</summary>
    public async Task<PlayerId?> FindAsync(DiscordUser discord, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var link = await db
            .PlayerDiscordLinks.FirstOrDefaultAsync(x => x.DiscordId == discord.Id, ct)
            .ConfigureAwait(false);

        if (link is null)
            return null;

        var username = Clip(discord.Username);

        if (link.DiscordUsername != username)
        {
            link.DiscordUsername = username;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return link.PlayerEntityId;
    }

    /// <summary>Whether a player already has a name, whatever its case.</summary>
    public async Task<bool> IsTakenAsync(string name, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var lower = name.Trim().ToLowerInvariant();

        return await db.Players.AnyAsync(x => x.Name.ToLower() == lower, ct).ConfigureAwait(false);
    }

    /// <summary>A hotel name to offer the Discord account: theirs, fitted to the rules and free.</summary>
    public async Task<string> SuggestNameAsync(DiscordUser discord, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var bases = new[] { discord.Username, discord.GlobalName ?? string.Empty }
            .Select(NameSuggestion.Fit)
            .Where(x => x.Length > 0)
            .Select(x => x.ToLowerInvariant()[..Math.Min(x.Length, 12)])
            .Append("player")
            .ToList();

        // The names already taken that a suggestion could clash with: those starting like one.
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var start in bases.Distinct())
        {
            var names = await db
                .Players.AsNoTracking()
                .Where(x => x.Name.ToLower().StartsWith(start))
                .Select(x => x.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            taken.UnionWith(names);
        }

        return NameSuggestion.Suggest(
            new DiscordCandidates(discord.Username, discord.GlobalName),
            name => taken.Contains(name) || !wordFilter.IsClean(name)
        );
    }

    /// <summary>A new player for the Discord account, linked to it; or why not.</summary>
    public async Task<NewPlayerResult> CreateAsync(
        DiscordUser discord,
        string name,
        AvatarGenderType gender,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        if (
            await db
                .PlayerDiscordLinks.AnyAsync(x => x.DiscordId == discord.Id, ct)
                .ConfigureAwait(false)
        )
            return NewPlayerResult.Refused("This Discord account already has a player.");

        var created = await accounts
            .CreateAsync(new NewPlayer(name, null, gender, null), ct)
            .ConfigureAwait(false);

        if (created.Created is not { } player)
            return created;

        db.PlayerDiscordLinks.Add(
            new PlayerDiscordLinkEntity
            {
                PlayerEntityId = player.Value,
                DiscordId = discord.Id,
                DiscordUsername = Clip(discord.Username),
                PlayerEntity = null!,
            }
        );
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Discord account {DiscordId} ({DiscordName}) signed up as player {PlayerId}",
            discord.Id,
            discord.Username,
            player
        );

        await owner.DiscordLinkedAsync(player, discord.Id, ct).ConfigureAwait(false);

        return created;
    }

    private static string Clip(string username) =>
        username.Length <= PlayerDiscordLinkEntity.DISCORD_NAME_MAX_LENGTH
            ? username
            : username[..PlayerDiscordLinkEntity.DISCORD_NAME_MAX_LENGTH];
}
