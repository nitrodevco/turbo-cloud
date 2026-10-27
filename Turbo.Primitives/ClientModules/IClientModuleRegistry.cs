using System;

namespace Turbo.Primitives.ClientModules;

/// <summary>
/// Where a plugin declares the client module it brings: a JavaScript ES module a client that
/// accepted <c>client.modules</c> loads and runs beside its own code. The server builds it from the
/// plugin's TypeScript sources under <c>client/src/</c> (no prebuilt JavaScript is taken) and the
/// gamedata host serves it under <c>/gamedata/plugins/&lt;key&gt;/&lt;key&gt;.js</c>, beside the
/// files of the plugin folder's <c>wwwroot/</c>; the server works out its address and SHA-256 and
/// sends the list to every client that asked, again whenever it changes. A plugin with
/// <c>client/src/index.tsx</c> (or <c>index.ts</c>) has its module without registering it. See
/// <c>docs/client-modules.md</c>.
/// </summary>
public interface IClientModuleRegistry
{
    /// <summary>
    /// Declares the plugin's module, built from <paramref name="entry"/>: <paramref name="key"/> is
    /// the plugin's own key, and the entry a <c>.ts</c> or <c>.tsx</c> file under the plugin
    /// folder's <c>client/src/</c> (<c>client/src/index.tsx</c>). The module is built (or taken from
    /// the server's cache) before this returns. Disposing the handle takes the module away;
    /// unloading the plugin does too. Throws <see cref="ArgumentException"/> for a key that is not
    /// the plugin's or an entry that is not a TypeScript file there, an exception naming each
    /// <c>file:line:column</c> when the module does not build, and
    /// <see cref="InvalidOperationException"/> for a key that already has a module, or a registry
    /// that is not a plugin's (core has no folder to build from).
    /// </summary>
    IDisposable Register(string key, string entry);
}
