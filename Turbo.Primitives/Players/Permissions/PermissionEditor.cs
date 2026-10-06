using System;
using System.Collections.Generic;
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
/// and a wildcard only when they hold every registered node it covers (an explicit-only node is
/// not covered by one);</item>
/// <item>a group that gives <c>permissions.superuser</c>, directly or through a parent, is only
/// for an editor who holds it.</item>
/// </list>
/// The heaviest groups therefore stay the console's to change. A holder of
/// <c>permissions.superuser</c> is bound by none of these limits, but still needs
/// <c>permissions.manage</c> and is audited like anyone; the console itself
/// (<see cref="ForConsole"/>) may change anything. Built by <see cref="IPermissionEditService"/>.
/// </summary>
public sealed class PermissionEditor
{
    private readonly IGrainFactory _grainFactory;
    private readonly PermissionRegistry _registry;
    private readonly ImmutableHashSet<string> _granted;

    public PermissionEditor(
        IGrainFactory grainFactory,
        PermissionRegistry registry,
        PermissionGroupDirectorySnapshot groups,
        ResolvedPermissionsSnapshot resolved
    )
        : this(grainFactory, registry, groups, resolved.Granted)
    {
        CanManage = resolved.Has(PermissionNodes.Permissions.MANAGE);
        IsSuperuser = CanManage && resolved.Has(PermissionNodes.Permissions.SUPERUSER);
        HeaviestWeight = HeaviestOf(groups, resolved);
    }

    private PermissionEditor(
        IGrainFactory grainFactory,
        PermissionRegistry registry,
        PermissionGroupDirectorySnapshot groups,
        ImmutableHashSet<string> granted
    )
    {
        _grainFactory = grainFactory;
        _registry = registry;
        _granted = granted;
        Groups = groups;
    }

    /// <summary>The server console: it owns the machine already, and may change anything.</summary>
    public static PermissionEditor ForConsole(
        IGrainFactory grainFactory,
        PermissionRegistry registry,
        PermissionGroupDirectorySnapshot groups
    ) =>
        new(grainFactory, registry, groups, [])
        {
            CanManage = true,
            IsSuperuser = true,
            HeaviestWeight = int.MaxValue,
        };

    public PermissionGroupDirectorySnapshot Groups { get; }

    public bool CanManage { get; private init; }

    /// <summary>
    /// Holds <c>permissions.manage</c> and <c>permissions.superuser</c>, or is the console: none of
    /// the weight and held-node limits apply.
    /// </summary>
    public bool IsSuperuser { get; private init; }

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

        if (group is null || IsSuperuser)
            return PermissionEditRefusal.None;

        // A lighter group that gives superuser would let its editor hand the node out through it.
        return CheckWeight(group.Weight) is not PermissionEditRefusal.None and var heavy ? heavy
            : GivesSuperuser(group) ? PermissionEditRefusal.NeedsSuperuser
            : PermissionEditRefusal.None;
    }

    /// <summary>Whether a group of <paramref name="weight"/> is the editor's to make or edit.</summary>
    public PermissionEditRefusal CheckWeight(int weight) =>
        !CanManage ? PermissionEditRefusal.NeedsManageNode
        : IsSuperuser || weight < HeaviestWeight ? PermissionEditRefusal.None
        : PermissionEditRefusal.GroupTooHeavy;

    /// <summary>Whether the editor may change <paramref name="target"/>'s own groups, nodes and meta.</summary>
    public async Task<PermissionEditRefusal> CheckPlayerAsync(PlayerId target, CancellationToken ct)
    {
        if (!CanManage)
            return PermissionEditRefusal.NeedsManageNode;

        if (IsSuperuser)
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
            IsSuperuser
            || !PermissionNodeFormat.IsValidAssignment(assignment)
            || PermissionGroupNames.IsGroupNode(assignment)
        )
            return PermissionEditRefusal.None;

        // A wildcard never reaches an explicit-only node, so it asks nothing of the editor.
        var held = PermissionNodeFormat.IsWildcard(assignment)
            ? _registry
                .Nodes.Values.Where(definition =>
                    !definition.ExplicitOnly
                    && PermissionNodeFormat.Specificity(assignment, definition.Node)
                        != PermissionNodeFormat.NO_MATCH
                )
                .All(definition => _granted.Contains(definition.Node))
            : _granted.Contains(assignment);

        return held ? PermissionEditRefusal.None : PermissionEditRefusal.NodeNotHeld;
    }

    /// <summary>
    /// Whether the group, or one it inherits from, assigns <c>permissions.superuser</c>. An expiry
    /// is not looked at: a group that gives it for a week is still not a lighter group's to hand
    /// out.
    /// </summary>
    private bool GivesSuperuser(PermissionGroupSnapshot group)
    {
        var visited = new HashSet<int>();
        var pending = new Stack<PermissionGroupSnapshot>([group]);

        while (pending.TryPop(out var next))
        {
            if (!visited.Add(next.Id))
                continue;

            if (
                next.Nodes.Any(x =>
                    x.Value
                    && string.Equals(
                        x.Node,
                        PermissionNodes.Permissions.SUPERUSER,
                        StringComparison.Ordinal
                    )
                )
            )
                return true;

            foreach (var parentId in next.ParentIds)
            {
                if (Groups.Groups.TryGetValue(parentId, out var parent))
                    pending.Push(parent);
            }
        }

        return false;
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
