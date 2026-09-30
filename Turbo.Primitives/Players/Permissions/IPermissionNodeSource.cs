using System.Collections.Generic;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// Contributes nodes and meta keys to the <see cref="PermissionRegistry"/>. Core has one
/// (<see cref="CorePermissionNodeSource"/>); a plugin adds its own.
/// </summary>
public interface IPermissionNodeSource
{
    /// <summary>
    /// The first segment every node and meta key from this source must start with: the plugin
    /// id. <c>null</c> for core only.
    /// </summary>
    string? Prefix { get; }

    IEnumerable<PermissionNodeDefinition> Nodes { get; }

    IEnumerable<PermissionMetaDefinition> MetaKeys { get; }
}
