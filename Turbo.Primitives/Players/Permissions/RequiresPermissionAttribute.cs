using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// Declares that a packet handler is for players holding a permission: any one of
/// <see cref="Nodes"/> lets the packet through. The handler's first step checks each node with
/// <c>IGrainFactory.HasPermissionAsync</c> and returns without acting otherwise. The attribute is
/// what a reviewer reads; <c>PermissionGateTests</c> fails the build when a handler declares a
/// node it never checks, or checks one it never declares. See <c>docs/permissions.md</c> §11.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresPermissionAttribute(params string[] nodes) : Attribute
{
    public IReadOnlyList<string> Nodes { get; } = nodes;
}
