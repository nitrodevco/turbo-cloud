using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Turbo.Plugins.Exceptions;

public enum PluginDependencyErrorType
{
    /// <summary>A declared dependency is not present in the discovered plugin set.</summary>
    Missing,

    /// <summary>The dependency graph contains a cycle and cannot be ordered.</summary>
    Cycle,

    /// <summary>The plugin cannot be reloaded or unloaded because live plugins depend on it.</summary>
    DependentsActive,

    /// <summary>The plugin cannot be reloaded because one of its dependencies is not live.</summary>
    DependencyInactive,
}

/// <summary>Raised when the plugin dependency graph prevents a load, reload or unload.</summary>
public sealed class PluginDependencyException : PluginException
{
    public PluginDependencyErrorType ErrorType { get; }

    /// <summary>The dependencies or dependents that caused the failure.</summary>
    public ImmutableArray<string> RelatedKeys { get; }

    public PluginDependencyException(
        PluginDependencyErrorType errorType,
        string? pluginKey = null,
        IEnumerable<string>? relatedKeys = null
    )
        : base(BuildMessage(errorType, pluginKey, relatedKeys?.ToImmutableArray() ?? []), pluginKey)
    {
        ErrorType = errorType;
        RelatedKeys = relatedKeys?.ToImmutableArray() ?? [];
    }

    private static string BuildMessage(
        PluginDependencyErrorType errorType,
        string? pluginKey,
        ImmutableArray<string> relatedKeys
    ) =>
        errorType switch
        {
            PluginDependencyErrorType.Missing =>
                $"Plugin '{pluginKey}' is missing dependency '{string.Join(", ", relatedKeys)}'.",
            PluginDependencyErrorType.Cycle => "Plugin dependencies contain a cycle.",
            PluginDependencyErrorType.DependentsActive =>
                $"Plugin '{pluginKey}' cannot be reloaded or unloaded while dependents are active: {string.Join(", ", relatedKeys)}.",
            PluginDependencyErrorType.DependencyInactive =>
                $"Plugin '{pluginKey}' cannot be reloaded because dependency '{string.Join(", ", relatedKeys)}' is not active.",
            _ => $"Plugin '{pluginKey}' has an unsatisfied dependency graph.",
        };
}
