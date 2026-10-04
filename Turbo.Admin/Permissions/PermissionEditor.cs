using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Admin.Permissions;

/// <summary>
/// One staff member about to change permissions in the panel, and what they may change. The rule
/// <c>docs/permissions.md</c> asks of any editor beyond the console: a manager may only grant
/// nodes they hold, and only touch groups lighter than their heaviest. So:
/// <list type="bullet">
/// <item>every change needs <c>permissions.manage</c>;</item>
/// <item>a group may be edited, joined, left, made a parent or created only when its weight is
/// below the editor's heaviest group;</item>
/// <item>a player may be edited only when their own heaviest group is lighter too, so nobody
/// edits themselves, their equals or their seniors;</item>
/// <item>a node may be set or unset (unsetting a denial grants it) only when the editor holds it,
/// and a wildcard only when they hold every registered node it covers.</item>
/// </list>
/// The heaviest groups therefore stay the console's to change. Built by
/// <see cref="PermissionEditPolicy"/>.
/// </summary>
public sealed class PermissionEditor
{
    private readonly IGrainFactory _grainFactory;
    private readonly PermissionRegistry _registry;
    private readonly ImmutableHashSet<string> _granted;

    internal PermissionEditor(
        IGrainFactory grainFactory,
        PermissionRegistry registry,
        PermissionGroupDirectorySnapshot groups,
        ResolvedPermissionsSnapshot resolved
    )
    {
        _grainFactory = grainFactory;
        _registry = registry;
        _granted = resolved.Granted;
        Groups = groups;
        CanManage = resolved.Has(PermissionNodes.Permissions.MANAGE);
        HeaviestWeight = HeaviestOf(groups, resolved);
    }

    public PermissionGroupDirectorySnapshot Groups { get; }

    public bool CanManage { get; }

    /// <summary>The weight of the heaviest group the editor reaches, directly or by inheritance.</summary>
    public int HeaviestWeight { get; }

    /// <summary>
    /// Whether the editor may change the group called <paramref name="name"/>. An unknown group is
    /// not refused here: the grain answers that it does not exist.
    /// </summary>
    public PermissionEditRefusal CheckGroup(string name)
    {
        if (!CanManage)
            return PermissionEditRefusal.NeedsManageNode;

        var group = Groups.Groups.Values.FirstOrDefault(x => x.Name == name);

        return group is null ? PermissionEditRefusal.None : CheckWeight(group.Weight);
    }

    /// <summary>Whether a group of <paramref name="weight"/> is the editor's to make or edit.</summary>
    public PermissionEditRefusal CheckWeight(int weight) =>
        !CanManage ? PermissionEditRefusal.NeedsManageNode
        : weight < HeaviestWeight ? PermissionEditRefusal.None
        : PermissionEditRefusal.GroupTooHeavy;

    /// <summary>Whether the editor may change <paramref name="target"/>'s own groups, nodes and meta.</summary>
    public async Task<PermissionEditRefusal> CheckPlayerAsync(PlayerId target, CancellationToken ct)
    {
        if (!CanManage)
            return PermissionEditRefusal.NeedsManageNode;

        var resolved = await _grainFactory
            .GetPlayerPermissionGrain(target)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);

        return HeaviestOf(Groups, resolved) < HeaviestWeight
            ? PermissionEditRefusal.None
            : PermissionEditRefusal.PlayerTooHeavy;
    }

    /// <summary>
    /// Whether the editor may set or unset <paramref name="assignment"/>: they hold the node, or
    /// every registered node the wildcard covers. A malformed or reserved one is not refused
    /// here: the grain answers why it is wrong.
    /// </summary>
    public PermissionEditRefusal CheckAssignment(string assignment)
    {
        if (
            !PermissionNodeFormat.IsValidAssignment(assignment)
            || PermissionGroupNames.IsGroupNode(assignment)
        )
            return PermissionEditRefusal.None;

        var held = PermissionNodeFormat.IsWildcard(assignment)
            ? _registry
                .Nodes.Keys.Where(node =>
                    PermissionNodeFormat.Specificity(assignment, node)
                    != PermissionNodeFormat.NO_MATCH
                )
                .All(_granted.Contains)
            : _granted.Contains(assignment);

        return held ? PermissionEditRefusal.None : PermissionEditRefusal.NodeNotHeld;
    }

    /// <summary>The heaviest group behind a resolved set: every group reached grants <c>group.&lt;name&gt;</c>.</summary>
    internal static int HeaviestOf(
        PermissionGroupDirectorySnapshot groups,
        ResolvedPermissionsSnapshot resolved
    ) =>
        groups
            .Groups.Values.Where(x => resolved.Has(PermissionGroupNames.ToNode(x.Name)))
            .Select(x => x.Weight)
            .DefaultIfEmpty(int.MinValue)
            .Max();
}
