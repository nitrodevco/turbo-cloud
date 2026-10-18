using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Players.Permissions;

/// <summary>
/// The grants of a plugin's permission nodes and meta keys (<see cref="IPluginDataOwner"/>):
/// every group's and player's assignment whose node or key is one of its roots or starts with one
/// (<see cref="PluginDataScope.NodeRoots"/>), wildcards and denials included. Found in the tables;
/// taken away through the group directory and the player grains, the only writers, so each removal
/// is audited and reaches the players online.
/// </summary>
internal sealed class PluginPermissionData(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory
) : IPluginDataOwner
{
    public async Task<ImmutableArray<PluginDataItem>> FindAsync(
        PluginDataScope plugin,
        CancellationToken ct
    ) =>
        [
            .. (await AssignmentsAsync(plugin, ct).ConfigureAwait(false))
                .GroupBy(x => (x.Name, x.Meta))
                .OrderBy(x => x.Key.Name, StringComparer.Ordinal)
                .Select(x => new PluginDataItem(
                    PluginDataKind.Grant,
                    x.Key.Name,
                    x.Count(),
                    Describe(x.Key.Meta, x)
                )),
        ];

    public async Task RemoveAsync(
        PluginDataScope plugin,
        ImmutableArray<PluginDataItem> found,
        PlayerId actor,
        CancellationToken ct
    )
    {
        // Matched by the roots again as well: only what is the plugin's now is taken away.
        var listed = found
            .Where(x => x.Kind == PluginDataKind.Grant)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);
        var directory = grainFactory.GetPermissionGroupDirectoryGrain();

        foreach (
            var x in (await AssignmentsAsync(plugin, ct).ConfigureAwait(false)).Where(x =>
                listed.Contains(x.Name)
            )
        )
        {
            if (x.Group is { } group)
                await (
                    x.Meta
                        ? directory.UnsetMetaAsync(group, x.Name, x.Temporary, actor, ct)
                        : directory.UnsetNodeAsync(group, x.Name, x.Temporary, actor, ct)
                ).ConfigureAwait(false);
            else
            {
                var player = grainFactory.GetPlayerPermissionGrain(x.Player!.Value);

                await (
                    x.Meta
                        ? player.UnsetMetaAsync(x.Name, x.Temporary, actor, ct)
                        : player.UnsetNodeAsync(x.Name, x.Temporary, actor, ct)
                ).ConfigureAwait(false);
            }
        }
    }

    private async Task<List<Assignment>> AssignmentsAsync(
        PluginDataScope plugin,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var found = new List<Assignment>();

        foreach (var root in plugin.NodeRoots)
        {
            var below = root + ".";

            found.AddRange(
                (
                    await dbCtx
                        .PermissionGroupNodes.AsNoTracking()
                        .Where(x => x.Node == root || x.Node.StartsWith(below))
                        .Join(
                            dbCtx.PermissionGroups,
                            x => x.GroupEntityId,
                            g => g.Id,
                            (x, g) =>
                                new
                                {
                                    x.Node,
                                    g.Name,
                                    x.IsTemporary,
                                }
                        )
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                ).Select(x => new Assignment(x.Node, false, x.Name, null, x.IsTemporary))
            );
            found.AddRange(
                (
                    await dbCtx
                        .PermissionGroupMeta.AsNoTracking()
                        .Where(x => x.Key == root || x.Key.StartsWith(below))
                        .Join(
                            dbCtx.PermissionGroups,
                            x => x.GroupEntityId,
                            g => g.Id,
                            (x, g) =>
                                new
                                {
                                    x.Key,
                                    g.Name,
                                    x.IsTemporary,
                                }
                        )
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                ).Select(x => new Assignment(x.Key, true, x.Name, null, x.IsTemporary))
            );
            found.AddRange(
                (
                    await dbCtx
                        .PlayerPermissionNodes.AsNoTracking()
                        .Where(x => x.Node == root || x.Node.StartsWith(below))
                        .Select(x => new
                        {
                            x.Node,
                            x.PlayerEntityId,
                            x.IsTemporary,
                        })
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                ).Select(x => new Assignment(
                    x.Node,
                    false,
                    null,
                    PlayerId.Parse(x.PlayerEntityId),
                    x.IsTemporary
                ))
            );
            found.AddRange(
                (
                    await dbCtx
                        .PlayerPermissionMeta.AsNoTracking()
                        .Where(x => x.Key == root || x.Key.StartsWith(below))
                        .Select(x => new
                        {
                            x.Key,
                            x.PlayerEntityId,
                            x.IsTemporary,
                        })
                        .ToListAsync(ct)
                        .ConfigureAwait(false)
                ).Select(x => new Assignment(
                    x.Key,
                    true,
                    null,
                    PlayerId.Parse(x.PlayerEntityId),
                    x.IsTemporary
                ))
            );
        }

        return found;
    }

    /// <summary>"the groups staff, vip and 3 players", "meta key of the group staff".</summary>
    private static string Describe(bool meta, IEnumerable<Assignment> holders)
    {
        var groups = holders
            .Select(x => x.Group)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var players = holders.Where(x => x.Group is null).Select(x => x.Player).Distinct().Count();
        var parts = new List<string>();

        if (groups.Count > 0)
            parts.Add(
                groups.Count == 1
                    ? $"the group {groups[0]}"
                    : $"the groups {string.Join(", ", groups)}"
            );

        if (players > 0)
            parts.Add(players == 1 ? "1 player" : $"{players} players");

        return (meta ? "meta key of " : "") + string.Join(" and ", parts);
    }

    /// <summary>One assignment: of a node or a meta key, to a group (by name) or else a player.</summary>
    private sealed record Assignment(
        string Name,
        bool Meta,
        string? Group,
        PlayerId? Player,
        bool Temporary
    );
}
