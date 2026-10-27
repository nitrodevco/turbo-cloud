using System.Collections.Generic;

namespace Turbo.Plugins.Exceptions;

/// <summary>
/// Raised when a plugin's client module does not build from its sources: esbuild's errors, each
/// <c>file:line:column: text</c> where it has a place (<c>client/src/index.tsx:3:16: ...</c>).
/// The plugin fails to load.
/// </summary>
public sealed class PluginClientBuildException(string pluginKey, IReadOnlyList<string> errors)
    : PluginException(
        $"The client module of {pluginKey} did not build:\n{string.Join('\n', errors)}",
        pluginKey
    );
