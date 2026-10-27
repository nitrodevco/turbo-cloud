using System.Collections.Generic;
using System.Linq;

namespace Turbo.Plugins.Exceptions;

/// <summary>
/// A plugin the server refuses to run: its folder holds an executable file, or its code breaks
/// the plugin policy (<c>docs/plugin-loading.md</c>). It is not tried again until its files change.
/// </summary>
public sealed class PluginRejectedException(string pluginKey, IReadOnlyList<string> reasons)
    : PluginException(Describe(reasons), pluginKey)
{
    /// <summary>Each reason, a file and what is wrong with it.</summary>
    public IReadOnlyList<string> Reasons { get; } = reasons;

    private static string Describe(IReadOnlyList<string> reasons) =>
        $"Refused: {string.Join("; ", reasons.Take(PluginCompileException.SHOWN))}{(reasons.Count > PluginCompileException.SHOWN ? $" (and {reasons.Count - PluginCompileException.SHOWN} more)" : "")}";
}
