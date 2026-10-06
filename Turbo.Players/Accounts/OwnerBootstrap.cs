using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Events;
using Turbo.Players.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Players.Accounts;

/// <summary>
/// <see cref="IOwnerBootstrap"/>: the owner is put in the <c>admin</c> group and given
/// <c>permissions.superuser</c> through the permission grain, so the change is saved and audited
/// like any other (as the console, which has no player). Granting twice changes nothing.
/// </summary>
public sealed class OwnerBootstrap(
    IGrainFactory grainFactory,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<OwnerConfig> config,
    IHostEnvironment environment,
    EventSystem eventSystem,
    ILogger<OwnerBootstrap> logger
) : IOwnerBootstrap
{
    /// <summary>The group an owner is in: the one seeded with every node.</summary>
    public const string OWNER_GROUP = "admin";

    public async Task<bool> PlayerCreatedAsync(PlayerId player, string name, CancellationToken ct)
    {
        try
        {
            var owner = config.Value;

            var isOwner = owner.NamesAnOwner
                ? IsNamed(owner, name)
                : owner.FirstPlayerInDevelopment
                    && environment.IsDevelopment()
                    && await IsOnlyPlayerAsync(ct).ConfigureAwait(false);

            if (!isOwner)
                return false;

            await GrantAsync(player, name, ct).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not check whether new player {Name} is the owner", name);

            return false;
        }
    }

    public async Task<bool> DiscordLinkedAsync(
        PlayerId player,
        string discordId,
        CancellationToken ct
    )
    {
        try
        {
            var named = config.Value.DiscordId.Trim();

            if (named.Length == 0 || !string.Equals(named, discordId, StringComparison.Ordinal))
                return false;

            await GrantAsync(player, await NameOfAsync(player, ct).ConfigureAwait(false), ct)
                .ConfigureAwait(false);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Could not make the player behind Discord {Id} the owner",
                discordId
            );

            return false;
        }
    }

    /// <summary>
    /// At startup: the named owner may have signed up before the configuration said so, or
    /// before this version. Finds them and makes sure they hold what an owner holds. Nothing
    /// happens when the owner has not signed up yet; sign-up does it then.
    /// </summary>
    public async Task EnsureExistingAsync(CancellationToken ct)
    {
        var owner = config.Value;

        if (!owner.NamesAnOwner)
            return;

        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var name = owner.Name.Trim().ToLowerInvariant();
        var discordId = owner.DiscordId.Trim();

        var found = await db
            .Players.AsNoTracking()
            .Where(x =>
                (name.Length > 0 && x.Name.ToLower() == name)
                || (
                    discordId.Length > 0
                    && db.PlayerDiscordLinks.Any(l =>
                        l.PlayerEntityId == x.Id && l.DiscordId == discordId
                    )
                )
            )
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var player in found)
            await GrantAsync(new PlayerId(player.Id), player.Name, ct).ConfigureAwait(false);

        if (found.Count == 0)
            logger.LogInformation(
                "The hotel's owner ({Owner}) has not signed up yet; they become the owner when they do",
                owner.Name.Trim().Length > 0 ? owner.Name.Trim() : "Discord " + discordId
            );
    }

    private static bool IsNamed(OwnerConfig owner, string name) =>
        owner.Name.Trim().Length > 0
        && string.Equals(owner.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);

    private async Task<bool> IsOnlyPlayerAsync(CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db.Players.CountAsync(ct).ConfigureAwait(false) == 1;
    }

    private async Task<string> NameOfAsync(PlayerId player, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db
                .Players.AsNoTracking()
                .Where(x => x.Id == player.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false)
            ?? player.Value.ToString();
    }

    private async Task GrantAsync(PlayerId player, string name, CancellationToken ct)
    {
        var grain = grainFactory.GetPlayerPermissionGrain(player);

        var group = await grain
            .AddGroupAsync(OWNER_GROUP, null, PermissionExpiryModeType.Replace, null, ct)
            .ConfigureAwait(false);
        var node = await grain
            .SetNodeAsync(
                PermissionNodes.Permissions.SUPERUSER,
                true,
                null,
                PermissionExpiryModeType.Replace,
                null,
                ct
            )
            .ConfigureAwait(false);

        if (!IsDone(group) || !IsDone(node))
        {
            logger.LogWarning(
                "Could not make {Name} the owner: joining {Group} answered {GroupResult}, setting {Node} answered {NodeResult}",
                name,
                OWNER_GROUP,
                group,
                PermissionNodes.Permissions.SUPERUSER,
                node
            );

            return;
        }

        var changed =
            group == PermissionChangeResultType.Changed
            || node == PermissionChangeResultType.Changed;

        if (changed)
            logger.LogWarning(
                "{Name} (player {PlayerId}) is now the hotel's owner: in the {Group} group, with {Node}",
                name,
                player,
                OWNER_GROUP,
                PermissionNodes.Permissions.SUPERUSER
            );

        try
        {
            await eventSystem
                .PublishAsync(
                    new OwnerConfirmedEvent
                    {
                        PlayerId = player,
                        Name = name,
                        Granted = changed,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "A handler of the owner being confirmed failed");
        }
    }

    private static bool IsDone(PermissionChangeResultType result) =>
        result is PermissionChangeResultType.Changed or PermissionChangeResultType.Unchanged;
}
