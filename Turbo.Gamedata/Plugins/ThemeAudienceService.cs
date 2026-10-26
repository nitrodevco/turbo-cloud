using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Entities.Players;
using Turbo.Gamedata.Files;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// <see cref="IThemeAudiences"/> over <c>gamedata_themes</c> (who gets each theme) and
/// <c>player_themes</c> (what each player chose), resolved by <see cref="ThemeChains"/>. A change
/// builds the client's variables again (the default theme's look is in the file every client
/// loads); a session's own theme's variables are built once per theme (<see cref="GamedataFileService.ThemeVariablesAsync"/>).
/// </summary>
internal sealed class ThemeAudienceService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    GamedataFileService files,
    LoadedPluginGamedata loaded,
    IGrainFactory grainFactory,
    TimeProvider time,
    ILogger<ThemeAudienceService> logger,
    IServiceProvider? host = null
) : IThemeAudiences
{
    private const int MAX_AUDIENCE = 500;

    public async Task<ImmutableArray<ThemeAudience>> ListAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return
        [
            .. (await ThemesAsync(dbCtx, ct).ConfigureAwait(false)).Values.OrderBy(
                x => x.Key,
                StringComparer.OrdinalIgnoreCase
            ),
        ];
    }

    public async Task<ThemeAudience> SetAsync(
        string key,
        ThemeAudienceChange change,
        PlayerId actor,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var themes = await ThemesAsync(dbCtx, ct).ConfigureAwait(false);

        if (!themes.TryGetValue(key, out var theme))
            throw new ArgumentException(
                $"There is no theme {key}: a theme is a plugin with a theme.json that is on.",
                nameof(key)
            );

        var status = change.Status ?? theme.Status;
        var isDefault = change.IsDefault ?? theme.IsDefault;

        if (isDefault && status != ThemeStatus.Live)
            throw new ArgumentException(
                $"The default theme is one every player gets: make {key} Live first.",
                nameof(change)
            );

        var players = change.Players ?? theme.Players;
        var groups = change.Groups ?? theme.Groups;

        if (players.Length + groups.Length > MAX_AUDIENCE)
            throw new ArgumentException(
                $"An audience lists {MAX_AUDIENCE} players and groups at most.",
                nameof(change)
            );

        if (change.Players is { } named && !named.IsEmpty)
        {
            var ids = named.Distinct().ToList();
            var found = await dbCtx
                .Players.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (ids.Except(found).FirstOrDefault() is var missing and > 0)
                throw new ArgumentException(
                    $"There is no player with the id {missing}.",
                    nameof(change)
                );

            players = [.. ids];
        }

        if (change.Groups is { } groupNames && !groupNames.IsEmpty)
        {
            var known = (
                await grainFactory
                    .GetPermissionGroupDirectoryGrain()
                    .GetSnapshotAsync(ct)
                    .ConfigureAwait(false)
            )
                .Groups.Values.Select(x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (groupNames.FirstOrDefault(x => !known.Contains(x)) is { } unknown)
                throw new ArgumentException(
                    $"There is no permission group {unknown}.",
                    nameof(change)
                );

            groups = [.. groupNames.Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        var rows = await dbCtx.GamedataThemes.ToListAsync(ct).ConfigureAwait(false);
        var row = rows.FirstOrDefault(x =>
            string.Equals(x.PluginKey, theme.Key, StringComparison.OrdinalIgnoreCase)
        );
        var now = time.GetUtcNow().UtcDateTime;

        if (row is null)
        {
            row = new GamedataThemeEntity { PluginKey = theme.Key };
            dbCtx.GamedataThemes.Add(row);
        }

        row.Status = status;
        row.IsDefault = isDefault;
        row.Selectable = change.Selectable ?? theme.Selectable;
        row.AudiencePlayers = ThemeRows.Write(players);
        row.AudienceGroups = ThemeRows.Write(groups);
        row.UpdatedAt = now;

        // One default: the last one is not any more.
        if (isDefault)
            foreach (var other in rows.Where(x => x != row && x.IsDefault))
            {
                other.IsDefault = false;
                other.UpdatedAt = now;
            }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        logger.LogInformation(
            "Theme {Key} set to {Status}{Default}{Selectable} by player {Actor}",
            theme.Key,
            row.Status,
            row.IsDefault ? ", the default" : string.Empty,
            row.Selectable ? ", may be chosen" : string.Empty,
            actor.Value
        );

        return ThemeRows.Of(theme.Key, theme.Extends, row);
    }

    public async Task<ThemeSession> SessionAsync(
        PlayerId player,
        string? requested,
        CancellationToken ct
    )
    {
        var (themes, chosen) = await ReadAsync(player, ct).ConfigureAwait(false);
        var (groups, mayPreview) = await WhoAsync(player, themes, ct).ConfigureAwait(false);
        var list = themes.Values.ToList();
        var theme = ThemeChains.Resolve(list, player.Value, groups, mayPreview, chosen, requested);
        var variables = await files.ThemeVariablesAsync(theme, ct).ConfigureAwait(false);
        var names = Names();

        return new ThemeSession(
            theme ?? string.Empty,
            variables,
            [
                // The default is the chooser's own "Hotel default".
                .. list.Where(x =>
                        !x.IsDefault && ThemeChains.MayChoose(x, player.Value, groups, mayPreview)
                    )
                    .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new ThemeChoice(x.Key, names.GetValueOrDefault(x.Key) ?? x.Key)),
            ],
            chosen is not null
            && themes.TryGetValue(chosen, out var own)
            && ThemeChains.MayChoose(own, player.Value, groups, mayPreview)
                ? own.Key
                : string.Empty
        );
    }

    public async Task ChooseAsync(PlayerId player, string key, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .PlayerThemes.FirstOrDefaultAsync(x => x.PlayerEntityId == player.Value, ct)
            .ConfigureAwait(false);

        if (string.IsNullOrEmpty(key))
        {
            if (row is not null)
            {
                dbCtx.PlayerThemes.Remove(row);
                await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return;
        }

        var themes = await ThemesAsync(dbCtx, ct).ConfigureAwait(false);
        var (groups, mayPreview) = await WhoAsync(player, themes, ct).ConfigureAwait(false);

        if (
            !themes.TryGetValue(key, out var theme)
            || !ThemeChains.MayChoose(theme, player.Value, groups, mayPreview)
        )
            throw new ArgumentException($"The theme {key} can't be chosen.", nameof(key));

        if (row is null)
            dbCtx.PlayerThemes.Add(
                new PlayerThemeEntity { PlayerEntityId = player.Value, PluginKey = theme.Key }
            );
        else
            row.PluginKey = theme.Key;

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, ThemeAudience>> ThemesAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    ) =>
        ThemeRows.Themes(
            loaded.Themes(),
            await ThemeRows.ReadAsync(dbCtx, ct).ConfigureAwait(false)
        );

    private async Task<(
        IReadOnlyDictionary<string, ThemeAudience> Themes,
        string? Chosen
    )> ReadAsync(PlayerId player, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var themes = await ThemesAsync(dbCtx, ct).ConfigureAwait(false);
        var chosen = await dbCtx
            .PlayerThemes.AsNoTracking()
            .Where(x => x.PlayerEntityId == player.Value)
            .Select(x => x.PluginKey)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return (themes, chosen);
    }

    /// <summary>
    /// The permission groups the player holds, by name, the ones they inherit too - asked only when
    /// a theme's audience names a group - and whether they may preview every development theme.
    /// </summary>
    private async Task<(IReadOnlySet<string> Groups, bool MayPreview)> WhoAsync(
        PlayerId player,
        IReadOnlyDictionary<string, ThemeAudience> themes,
        CancellationToken ct
    )
    {
        var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // No player (a ticket that is nobody's): the default, like any visitor.
        if (player.Value <= 0)
            return (groups, false);

        var mayPreview = await grainFactory
            .HasPermissionAsync(player, PermissionNodes.Appearance.THEMES_PREVIEW, ct)
            .ConfigureAwait(false);

        if (!themes.Values.Any(x => x.Status == ThemeStatus.Development && !x.Groups.IsEmpty))
            return (groups, mayPreview);

        var held = await grainFactory
            .GetPlayerPermissionGrain(player)
            .GetAssignmentsAsync(ct)
            .ConfigureAwait(false);
        var directory = await grainFactory
            .GetPermissionGroupDirectoryGrain()
            .GetSnapshotAsync(ct)
            .ConfigureAwait(false);
        var now = time.GetUtcNow().UtcDateTime;
        var pending = new Stack<int>(
            held.Groups.Where(x => x.ExpiresAt is null || x.ExpiresAt > now).Select(x => x.GroupId)
        );
        var seen = new HashSet<int>();

        while (pending.TryPop(out var id))
        {
            if (!seen.Add(id) || !directory.Groups.TryGetValue(id, out var group))
                continue;

            groups.Add(group.Name);

            foreach (var parent in group.ParentIds)
                pending.Push(parent);
        }

        return (groups, mayPreview);
    }

    /// <summary>Each theme's name to show: its plugin's name; the hotel's own is "This hotel".</summary>
    private Dictionary<string, string> Names()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ThemeChains.HOTEL] = "This hotel",
        };

        foreach (var plugin in host?.GetService<IPluginAdministration>()?.List() ?? [])
            if (!string.IsNullOrWhiteSpace(plugin.Name))
                names[plugin.Key] = plugin.Name;

        return names;
    }
}
