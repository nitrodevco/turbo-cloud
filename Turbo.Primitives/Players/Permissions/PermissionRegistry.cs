using System;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// Every node a check may ask about and every meta key a value may be stored under, collected
/// from the <see cref="IPermissionNodeSource"/>s at startup. Wildcards expand against it, and an
/// assignment of something it does not know is reported rather than dropped. Built once and
/// read-only afterwards.
/// </summary>
public sealed class PermissionRegistry
{
    private readonly Dictionary<string, PermissionNodeDefinition> _nodes = new(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, PermissionMetaDefinition> _metaKeys = new(
        StringComparer.Ordinal
    );

    /// <exception cref="InvalidOperationException">
    /// A node or key is malformed or registered twice, or a plugin source registers outside its
    /// prefix or claims a prefix core already uses.
    /// </exception>
    public PermissionRegistry(IEnumerable<IPermissionNodeSource> sources)
    {
        var all = sources.ToList();

        // Core's first segments are reserved, so a plugin called "room" cannot register
        // room.anything and pass for core.
        var coreRoots = all.Where(x => x.Prefix is null)
            .SelectMany(x => x.Nodes.Select(n => n.Node).Concat(x.MetaKeys.Select(m => m.Key)))
            .Where(PermissionNodeFormat.IsValidNode)
            .Select(PermissionNodeFormat.RootOf)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var source in all)
        {
            var prefix = source.Prefix;

            if (prefix is not null)
            {
                if (!PermissionNodeFormat.IsValidSegment(prefix))
                    throw new InvalidOperationException(
                        $"Permission source prefix '{prefix}' is not a single lowercase segment."
                    );

                if (coreRoots.Contains(prefix))
                    throw new InvalidOperationException(
                        $"Permission source prefix '{prefix}' is reserved by core."
                    );
            }

            foreach (var definition in source.Nodes)
            {
                Validate(definition.Node, prefix, "node");

                if (definition.GrantedByDefault && definition.ClientLevel is not null)
                    throw new InvalidOperationException(
                        $"Permission node '{definition.Node}' is granted by default and has a client level, which would raise every player's security level."
                    );

                if (definition.GrantedByDefault && definition.ExplicitOnly)
                    throw new InvalidOperationException(
                        $"Permission node '{definition.Node}' is granted by default and explicit only, which cannot both be true."
                    );

                if (!_nodes.TryAdd(definition.Node, definition))
                    throw new InvalidOperationException(
                        $"Permission node '{definition.Node}' is registered twice."
                    );
            }

            foreach (var definition in source.MetaKeys)
            {
                Validate(definition.Key, prefix, "meta key");

                if (!_metaKeys.TryAdd(definition.Key, definition))
                    throw new InvalidOperationException(
                        $"Permission meta key '{definition.Key}' is registered twice."
                    );
            }
        }
    }

    public IReadOnlyDictionary<string, PermissionNodeDefinition> Nodes => _nodes;

    public IReadOnlyDictionary<string, PermissionMetaDefinition> MetaKeys => _metaKeys;

    public bool IsRegistered(string node) => _nodes.ContainsKey(node);

    /// <summary>
    /// Whether a check may ask about <paramref name="node"/>: a registered node, or a membership
    /// node (<c>group.&lt;name&gt;</c>), which every group implies without registering.
    /// </summary>
    public bool IsCheckable(string node) =>
        IsRegistered(node)
        || (PermissionGroupNames.IsGroupNode(node) && PermissionNodeFormat.IsValidNode(node));

    public bool IsRegisteredMetaKey(string key) => _metaKeys.ContainsKey(key);

    private static void Validate(string value, string? prefix, string kind)
    {
        if (!PermissionNodeFormat.IsValidNode(value))
            throw new InvalidOperationException($"Permission {kind} '{value}' is malformed.");

        if (PermissionGroupNames.IsGroupNode(value))
            throw new InvalidOperationException(
                $"Permission {kind} '{value}' is under '{PermissionGroupNames.NODE_ROOT}.', which membership nodes reserve."
            );

        if (prefix is not null && !value.StartsWith(prefix + ".", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Permission {kind} '{value}' does not start with its source prefix '{prefix}.'."
            );
    }
}
