using System;

namespace Turbo.Primitives.ClientModules;

/// <summary>
/// Where a plugin declares the client modules it brings: JavaScript ES modules a client that
/// accepted <c>client.modules</c> loads and runs beside its own code. The files lie in the plugin
/// folder's <c>wwwroot/</c> and are served by the gamedata host under
/// <c>/gamedata/plugins/&lt;key&gt;/</c>; the server works out each module's address and SHA-256 and
/// sends the list to every client that asked, again whenever it changes. See
/// <c>docs/client-modules.md</c>.
/// </summary>
public interface IClientModuleRegistry
{
    /// <summary>
    /// Declares the plugin's module: <paramref name="key"/> is the plugin's own key, and
    /// <paramref name="relativePath"/> its entry file under the plugin folder's <c>wwwroot/</c>
    /// (<c>hello.js</c>). Disposing the handle takes the module away; unloading the plugin does too.
    /// Throws <see cref="ArgumentException"/> for a key that is not the plugin's or a path outside
    /// <c>wwwroot/</c> or not there, and <see cref="InvalidOperationException"/> for a key that
    /// already has a module, or a registry that is not a plugin's (core has no folder to serve from).
    /// </summary>
    IDisposable Register(string key, string relativePath);
}
