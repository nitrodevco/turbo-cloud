using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// One staff member about to change permissions, and what they may change: the rule
/// <c>docs/permissions.md</c> asks of every editor beyond the console, whether the change comes
/// from the admin panel or <c>:group</c>. So:
/// <list type="bullet">
/// <item>every change needs <c>permissions.manage</c>;</item>
/// <item>a group may be edited, joined, left, made a parent or created only when its weight is
/// below the editor's heaviest group;</item>
/// <item>a player may be edited only when their own heaviest group is lighter too, so nobody
/// edits themselves, their equals or their seniors;</item>
/// <item>a node may be set or unset (unsetting a denial grants it) only when the editor holds it,
/// and a wildcard only when they hold every registered node it covers.</item>
/// </list>
/// The heaviest groups therefore stay the console's to change; the console itself
/// (<see cref="ForConsole"/>) may change anything. Built by <see cref="IPermissionEditService"/>.
/// </summary>
public sealed class PermissionEditor
{
    private readonly IGrainFactory _grainFactory;
    private readonly PermissionRegistry _registry;
    private readonly ImmutableHashSet<string> _granted;
    private readonly bool _isConsole;

    public PermissionEditor(
        IGrainFactory grainFactory,
        PermissionRegistry registry,
        PermissionGroupDirectorySnapshot groups,
        ResolvedPermissionsSnapshot resolved
    )
        : this(grainFactory, registry, groups, resolved.Granted, false)
    {
        CanManage = resolved.Has(PermissionNodes.Permissions.MANAGE);
        HeaviestWeight = HeaviestOf(groups, resolved);
    }

    private PermissionEditor(
        IGrainFactory grainFactory,
        PermissionRegistry registry,
        PermissionGroupDirectorySnapshot groups,
        ImmutableHashSet<string> granted,
        bool isConsole
    )
    {
        _grainFactory = grainFactory;
        _registry = registry;
        _granted = granted;
        _isConsole = isConsole;
        Groups = groups;
    }

    /// <summary>The server console: it owns the machine already, and may change anything.</summary>
    public static PermissionEditor ForConsole(
        IGrainFactory grainFactory,
        PermissionRegistry registry,
        PermissionGroupDirectorySnapshot groups
    ) =>
        new(grainFactory, registry, groups, [], true)
        {
            CanManage = true,
            HeaviestWeight = int.MaxValue,
        };

    public PermissionGroupDirectorySnapshot Groups { get; }

    public bool CanManage { get; private init; }

    /// <summary>The weight of the heaviest group the editor reaches, directly or by inheritance.</summary>
    public int HeaviestWeight { get; private init; }

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
        : _isConsole || weight < HeaviestWeight ? PermissionEditRefusal.None
        : PermissionEditRefusal.GroupTooHeavy;

    /// <summary>Whether the editor may change <paramref name="target"/>'s own groups, nodes and meta.</summary>
    public async Task<PermissionEditRefusal> CheckPlayerAsync(PlayerId target, CancellationToken ct)
    {
        if (!CanManage)
            return PermissionEditRefusal.NeedsManageNode;

        if (_isConsole)
            return PermissionEditRefusal.None;

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
            _isConsole
            || !PermissionNodeFormat.IsValidAssignment(assignment)
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
    public static int HeaviestOf(
        PermissionGroupDirectorySnapshot groups,
        ResolvedPermissionsSnapshot resolved
    ) =>
        groups
            .Groups.Values.Where(x => resolved.Has(PermissionGroupNames.ToNode(x.Name)))
            .Select(x => x.Weight)
            .DefaultIfEmpty(int.MinValue)
            .Max();
}
