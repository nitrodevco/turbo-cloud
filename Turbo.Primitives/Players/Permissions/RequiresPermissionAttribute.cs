using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// Declares that a packet handler or a command is for players holding a permission: any one of
/// <see cref="Nodes"/> lets it through. For a packet handler the pipeline checks it
/// (<c>PermissionGate</c>) before the handler runs, and skips the packet without a reply
/// otherwise; the handler does not check it again. <c>PermissionGateTests</c> fails the build when
/// a handler declares a node nobody registered, or calls <c>HasPermissionAsync</c> itself. See
/// <c>docs/permissions.md</c> §11.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresPermissionAttribute(params string[] nodes) : Attribute
{
    public IReadOnlyList<string> Nodes { get; } = nodes;
}
