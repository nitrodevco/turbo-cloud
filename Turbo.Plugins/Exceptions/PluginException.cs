using System;

namespace Turbo.Plugins.Exceptions;

/// <summary>
/// Base type for plugin discovery, loading and lifecycle failures. Callers that want to treat any
/// plugin problem uniformly (for example, skipping a bad plugin folder) can catch this type.
/// </summary>
public abstract class PluginException(
    string message,
    string? pluginKey = null,
    Exception? innerException = null
) : Exception(message, innerException)
{
    /// <summary>The plugin key this failure relates to, when it is known.</summary>
    public string? PluginKey { get; } = pluginKey;
}
