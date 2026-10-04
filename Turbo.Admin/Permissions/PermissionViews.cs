using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Admin.Permissions;

/// <summary>
/// The panel's reads of permissions: the directory's groups and a player's grain, turned into the
/// API's shapes with names put to ids. Nothing here changes anything; the same reads the
/// <c>perm</c> console command prints.
/// </summary>
public sealed class PermissionViews(
    IGrainFactory grainFactory,
    IPermissionRegistryProvider registryProvider,
    TimeProvider timeProvider
)
{
    private IPermissionGroupDirectoryGrain Directory =>
        grainFactory.GetPermissionGroupDirectoryGrain();

    private PermissionRegistry Registry => registryProvider.Current;

    public PermissionGroupsResponse ListGroups(PermissionEditor editor)
    {
        var groups = editor.Groups.Groups;

        return new PermissionGroupsResponse(
            [
                .. groups
                    .Values.OrderByDescending(x => x.Weight)
                    .ThenBy(x => x.Name, StringComparer.Ordinal)
                    .Select(group => new PermissionGroupListItem(
                        group.Id,
                        group.Name,
                        group.DisplayName,
                        group.Weight,
                        [.. group.ParentIds.Select(id => NameOf(groups, id))],
                        group.Nodes.Length,
                        group.Meta.Length,
                        editor.CheckGroup(group.Name) == PermissionEditRefusal.None
                    )),
            ],
            editor.CanManage,
            editor.HeaviestWeight
        );
    }

    public PermissionGroupResponse? GetGroup(PermissionEditor editor, string name)
    {
        var groups = editor.Groups.Groups;
        var group = groups.Values.FirstOrDefault(x => x.Name == name);

        if (group is null)
            return null;

        // The level is a property of the whole inheritance: worked out for a player holding just
        // this group, as `perm group <g> info` does.
        var member = new PlayerPermissionAssignmentsSnapshot
        {
            Groups = [new PermissionGroupMembershipSnapshot { GroupId = group.Id }],
            Nodes = [],
            Meta = [],
        };
        var resolved = PermissionResolver.Resolve(Registry, groups, member, UtcNow);

        return new PermissionGroupResponse(
            group.Id,
            group.Name,
            group.DisplayName,
            group.Weight,
            group.Name == PermissionGroupNames.DEFAULT,
            [.. group.ParentIds.Where(groups.ContainsKey).Select(id => RefOf(groups[id]))],
            [
                .. groups
                    .Values.Where(x => x.ParentIds.Contains(group.Id))
                    .OrderByDescending(x => x.Weight)
                    .Select(RefOf),
            ],
            NodesOf(group.Nodes),
            MetaOf(group.Meta),
            LevelOf(resolved),
            editor.CheckGroup(group.Name) == PermissionEditRefusal.None
        );
    }

    public async Task<ImmutableArray<PermissionMemberView>> GetMembersAsync(
        string group,
        int count,
        CancellationToken ct
    )
    {
        var members = await Directory.GetMembersAsync(group, count, ct).ConfigureAwait(false);
        var names = await NamesAsync(members.Select(x => x.PlayerId), ct).ConfigureAwait(false);

        return
        [
            .. members.Select(x => new PermissionMemberView(
                x.PlayerId.Value,
                NameOf(names, x.PlayerId),
                x.ExpiresAt
            )),
        ];
    }

    /// <summary>
    /// The players in any group besides default, with their groups: the hotel's staff, as near as
    /// the permissions say. Up to <paramref name="perGroup"/> from each group.
    /// </summary>
    public async Task<ImmutableArray<PermissionStaffMember>> GetStaffAsync(
        PermissionEditor editor,
        int perGroup,
        CancellationToken ct
    )
    {
        var byPlayer = new Dictionary<PlayerId, List<PlayerGroupView>>();

        foreach (
            var group in editor.Groups.Groups.Values.Where(x =>
                x.Name != PermissionGroupNames.DEFAULT
            )
        )
        {
            var members = await Directory
                .GetMembersAsync(group.Name, perGroup, ct)
                .ConfigureAwait(false);

            foreach (var member in members)
            {
                if (!byPlayer.TryGetValue(member.PlayerId, out var list))
                    byPlayer[member.PlayerId] = list = [];

                list.Add(
                    new PlayerGroupView(
                        group.Name,
                        group.DisplayName,
                        group.Weight,
                        member.ExpiresAt
                    )
                );
            }
        }

        var names = await NamesAsync(byPlayer.Keys, ct).ConfigureAwait(false);

        return
        [
            .. byPlayer
                .Select(x => new PermissionStaffMember(
                    x.Key.Value,
                    NameOf(names, x.Key),
                    [.. x.Value.OrderByDescending(g => g.Weight)]
                ))
                .OrderByDescending(x => x.Groups[0].Weight)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
        ];
    }

    public async Task<PlayerPermissionsResponse> GetPlayerAsync(
        PermissionEditor editor,
        PlayerId playerId,
        string name,
        CancellationToken ct
    )
    {
        var grain = grainFactory.GetPlayerPermissionGrain(playerId);
        var assignments = await grain.GetAssignmentsAsync(ct).ConfigureAwait(false);
        var resolved = await grain.GetResolvedAsync(ct).ConfigureAwait(false);
        var client = await grain.GetClientStateAsync(ct).ConfigureAwait(false);
        var canEdit =
            await editor.CheckPlayerAsync(playerId, ct).ConfigureAwait(false)
            == PermissionEditRefusal.None;
        var groups = editor.Groups.Groups;

        return new PlayerPermissionsResponse(
            playerId.Value,
            name,
            [
                .. assignments
                    .Groups.Where(x => groups.ContainsKey(x.GroupId))
                    .Select(x =>
                    {
                        var group = groups[x.GroupId];

                        return new PlayerGroupView(
                            group.Name,
                            group.DisplayName,
                            group.Weight,
                            x.ExpiresAt
                        );
                    })
                    .OrderByDescending(x => x.Weight),
            ],
            [
                .. groups
                    .Values.Where(x => resolved.Has(PermissionGroupNames.ToNode(x.Name)))
                    .OrderByDescending(x => x.Weight)
                    .Select(RefOf),
            ],
            NodesOf(assignments.Nodes),
            MetaOf(assignments.Meta),
            [.. resolved.Granted.Order(StringComparer.Ordinal)],
            resolved.Meta,
            [.. resolved.UnregisteredNodes.Concat(resolved.UnregisteredMetaKeys)],
            new PermissionClientView(
                client.SecurityLevel.ToString(),
                (int)client.SecurityLevel,
                client.IsAmbassador,
                client.IsModerator,
                [.. client.Perks.Where(x => x.IsAllowed).Select(x => x.Perk.ToString())]
            ),
            LevelOf(resolved),
            canEdit
        );
    }

    public static PermissionCheckResponse CheckOf(PermissionCheckSnapshot check) =>
        new(
            check.Node,
            check.IsRegistered,
            check.Granted,
            check.Decision is { } decision ? SourceOf(decision) : null,
            [.. check.Overridden.Select(SourceOf)]
        );

    public PermissionCatalogResponse GetCatalog() =>
        new(
            [
                .. Registry
                    .Nodes.Values.OrderBy(x => x.Node, StringComparer.Ordinal)
                    .Select(x => new PermissionNodeDefinitionView(
                        x.Node,
                        x.Description,
                        x.ClientLevel is { } level ? (int)level : null,
                        x.GrantedByDefault
                    )),
            ],
            [
                .. Registry
                    .MetaKeys.Values.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => new PermissionMetaKeyView(
                        x.Key,
                        x.Description,
                        x.Selection.ToString()
                    )),
            ]
        );

    public async Task<ImmutableArray<PermissionHolderView>> FindHoldersAsync(
        string node,
        int count,
        CancellationToken ct
    )
    {
        var holders = await Directory.FindNodeHoldersAsync(node, count, ct).ConfigureAwait(false);
        var names = await TargetNamesAsync(holders.Select(x => (x.TargetType, x.TargetId)), ct)
            .ConfigureAwait(false);

        return
        [
            .. holders.Select(x => new PermissionHolderView(
                x.TargetType.ToString(),
                x.TargetId,
                names[(x.TargetType, x.TargetId)],
                x.Assignment.Node,
                x.Assignment.Value,
                x.Assignment.ExpiresAt
            )),
        ];
    }

    /// <summary>Audit rows with names put to whom they are about and who made them.</summary>
    public async Task<ImmutableArray<PermissionAuditEntry>> NameAuditAsync(
        ImmutableArray<PermissionAuditSnapshot> rows,
        CancellationToken ct
    )
    {
        var targets = await TargetNamesAsync(rows.Select(x => (x.TargetType, x.TargetId)), ct)
            .ConfigureAwait(false);
        var actors = await NamesAsync(
                rows.Where(x => x.ActorPlayerId is not null).Select(x => x.ActorPlayerId!.Value),
                ct
            )
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(x => new PermissionAuditEntry(
                x.CreatedAt,
                x.ActorPlayerId?.Value,
                x.ActorPlayerId is { } actor ? NameOf(actors, actor) : null,
                x.TargetType.ToString(),
                x.TargetId,
                targets[(x.TargetType, x.TargetId)],
                x.Action.ToString(),
                x.Subject,
                x.Value,
                x.ExpiresAt
            )),
        ];
    }

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    private ImmutableArray<PermissionNodeView> NodesOf(
        ImmutableArray<PermissionNodeAssignmentSnapshot> nodes
    ) =>
        [
            .. nodes
                .OrderBy(x => x.Node, StringComparer.Ordinal)
                .ThenBy(x => x.ExpiresAt is not null)
                .Select(x => new PermissionNodeView(
                    x.Node,
                    x.Value,
                    x.ExpiresAt,
                    Registry.Nodes.TryGetValue(x.Node, out var definition)
                        ? definition.Description
                        : null
                )),
        ];

    private ImmutableArray<PermissionMetaView> MetaOf(
        ImmutableArray<PermissionMetaAssignmentSnapshot> meta
    ) =>
        [
            .. meta.OrderBy(x => x.Key, StringComparer.Ordinal)
                .ThenBy(x => x.ExpiresAt is not null)
                .Select(x => new PermissionMetaView(
                    x.Key,
                    x.Value,
                    x.ExpiresAt,
                    Registry.MetaKeys.TryGetValue(x.Key, out var definition)
                        ? definition.Description
                        : null
                )),
        ];

    private PermissionLevelView LevelOf(ResolvedPermissionsSnapshot resolved)
    {
        var report = PermissionProjection.ReportLevel(Registry, resolved);

        return new PermissionLevelView(
            report.Level.ToString(),
            (int)report.Level,
            report.Source,
            [
                .. report.ShownButRefused.Select(x => new PermissionLevelNode(
                    x.Node,
                    x.Description,
                    (int)(x.ClientLevel ?? SecurityLevelType.None)
                )),
            ]
        );
    }

    private static PermissionSourceView SourceOf(PermissionAssignmentSourceSnapshot source) =>
        new(
            source.SourceType == PermissionSourceType.Player ? "player" : "group",
            source.GroupName,
            source.GroupWeight,
            source.Path,
            source.Node,
            source.Value,
            source.ExpiresAt
        );

    private static PermissionGroupRef RefOf(PermissionGroupSnapshot group) =>
        new(group.Name, group.DisplayName, group.Weight);

    private static string NameOf(
        ImmutableDictionary<int, PermissionGroupSnapshot> groups,
        int id
    ) => groups.TryGetValue(id, out var group) ? group.Name : $"#{id}";

    private static string NameOf(ImmutableDictionary<PlayerId, string> names, PlayerId id) =>
        names.GetValueOrDefault(id, $"#{id.Value}");

    private async Task<ImmutableDictionary<PlayerId, string>> NamesAsync(
        IEnumerable<PlayerId> players,
        CancellationToken ct
    )
    {
        var ids = players.Distinct().ToList();

        return ids.Count == 0
            ? ImmutableDictionary<PlayerId, string>.Empty
            : await grainFactory
                .GetPlayerDirectoryGrain()
                .GetPlayerNamesAsync(ids, ct)
                .ConfigureAwait(false);
    }

    private async Task<
        ImmutableDictionary<(PermissionAuditTargetType, int), string>
    > TargetNamesAsync(
        IEnumerable<(PermissionAuditTargetType Type, int Id)> targets,
        CancellationToken ct
    )
    {
        var all = targets.Distinct().ToList();
        var groups = (await Directory.GetSnapshotAsync(ct).ConfigureAwait(false)).Groups;
        var players = await NamesAsync(
                all.Where(x => x.Type == PermissionAuditTargetType.Player)
                    .Select(x => PlayerId.Parse(x.Id)),
                ct
            )
            .ConfigureAwait(false);

        return all.ToImmutableDictionary(
            x => (x.Type, x.Id),
            x =>
                x.Type == PermissionAuditTargetType.Group
                    ? NameOf(groups, x.Id)
                    : NameOf(players, PlayerId.Parse(x.Id))
        );
    }
}
