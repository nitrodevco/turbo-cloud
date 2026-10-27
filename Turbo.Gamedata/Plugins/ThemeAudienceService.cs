using System;
using System.Collections.Concurrent;
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

        if (ThemeRows.IsAlwaysLive(theme.Key) && status != ThemeStatus.Live)
            throw new ArgumentException(
                $"{theme.Key} is the theme every other extends: it is always Live.",
                nameof(change)
            );

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

    /// <summary>
    /// The first start after the migration that made the table: every theme loaded now was every
    /// player's before themes had an audience, so each is kept Live with a row of its own, and the
    /// migration's <see cref="ThemeRows.ADOPT"/> row goes. A theme loaded later with no row is in
    /// development. Does nothing once done.
    /// </summary>
    public async Task AdoptAsync(CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var rows = await dbCtx.GamedataThemes.ToListAsync(ct).ConfigureAwait(false);

        if (rows.FirstOrDefault(x => x.PluginKey == ThemeRows.ADOPT) is not { } adopt)
            return;

        var now = time.GetUtcNow().UtcDateTime;
        var kept = new List<string>();

        foreach (var key in loaded.Themes().Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (
                ThemeRows.IsAlwaysLive(key)
                || rows.Any(x =>
                    string.Equals(x.PluginKey, key, StringComparison.OrdinalIgnoreCase)
                )
            )
                continue;

            dbCtx.GamedataThemes.Add(
                new GamedataThemeEntity
                {
                    PluginKey = key,
                    Status = ThemeStatus.Live,
                    UpdatedAt = now,
                }
            );
            kept.Add(key);
        }

        dbCtx.GamedataThemes.Remove(adopt);
        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);
        files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);

        logger.LogInformation(
            "Themes the hotel had before they had an audience are kept Live: {Themes}; a theme added from now on starts in development",
            kept.Count == 0 ? "none" : string.Join(", ", kept)
        );
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

        // What this client draws, for live reload to send it only a look it does not have.
        if (player.Value > 0)
            NoteDrawn(
                player.Value,
                string.Equals(theme, requested, StringComparison.OrdinalIgnoreCase)
                    ? requested
                    : null,
                (await files.ThemeLookAsync(theme, ct).ConfigureAwait(false)).Revision
            );

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

    /// <summary>
    /// The look each player's client draws, by player id (<see cref="ThemeLiveReload"/>): the theme
    /// staff asked to preview for the load, and the look's revision. A player with none draws the
    /// look every client loads.
    /// </summary>
    private readonly ConcurrentDictionary<int, DrawnLook> _drawn = new();

    internal sealed record DrawnLook(string? Requested, string Revision);

    internal DrawnLook? DrawnBy(int player) => _drawn.GetValueOrDefault(player);

    internal void NoteDrawn(int player, string? requested, string revision) =>
        _drawn[player] = new DrawnLook(requested, revision);

    /// <summary>Forgets the looks of players not online any more: their next load asks again.</summary>
    internal void ForgetDrawn(IReadOnlySet<int> online)
    {
        foreach (var player in _drawn.Keys)
            if (!online.Contains(player))
                _drawn.TryRemove(player, out _);
    }

    /// <summary>
    /// The theme each of <paramref name="players"/> draws now (null: the hotel's look with no theme
    /// of its own), as <see cref="SessionAsync"/> works it out with the preview they loaded, read in
    /// one go; and the default theme, what a client with no look of its own draws.
    /// </summary>
    internal async Task<(
        string? Default,
        IReadOnlyList<(PlayerId Player, string? Theme)> Players
    )> ResolveAsync(IReadOnlyCollection<PlayerId> players, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var themes = await ThemesAsync(dbCtx, ct).ConfigureAwait(false);
        var list = themes.Values.ToList();
        var ids = players.Select(x => x.Value).ToList();
        var chosen =
            ids.Count == 0
                ? []
                : await dbCtx
                    .PlayerThemes.AsNoTracking()
                    .Where(x => ids.Contains(x.PlayerEntityId))
                    .ToDictionaryAsync(x => x.PlayerEntityId, x => x.PluginKey, ct)
                    .ConfigureAwait(false);

        var resolved = await Task.WhenAll(
                players.Select(async player =>
                {
                    var (groups, mayPreview) = await WhoAsync(player, themes, ct)
                        .ConfigureAwait(false);

                    return (
                        player,
                        ThemeChains.Resolve(
                            list,
                            player.Value,
                            groups,
                            mayPreview,
                            chosen.GetValueOrDefault(player.Value),
                            DrawnBy(player.Value)?.Requested
                        )
                    );
                })
            )
            .ConfigureAwait(false);

        return (ThemeChains.DefaultOf(list), resolved);
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
