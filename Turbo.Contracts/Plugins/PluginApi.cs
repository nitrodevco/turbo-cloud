namespace Turbo.Contracts.Plugins;

/// <summary>
/// The version of what plugins build against (these contracts and the host services they are
/// handed). It goes up when plugins get something new to build on; a plugin declares the one it
/// was built for in its manifest's <see cref="PluginManifest.ApiVersion"/>, and a server takes
/// plugins built for <see cref="OLDEST"/> up to its own <see cref="VERSION"/>.
/// </summary>
public static class PluginApi
{
    /// <summary>
    /// Everything plugins build on: these contracts and the host services they are handed. No
    /// plugin outside this project has been published, so it stays 1 while it changes, and counts
    /// up from the first one that is.
    /// </summary>
    public const int VERSION = 1;

    /// <summary>The oldest plugin API this server still loads.</summary>
    public const int OLDEST = 1;

    /// <summary>Why the server can't take a plugin built for <paramref name="manifest"/>'s API; null when it can.</summary>
    public static string? Refusal(PluginManifest manifest) =>
        manifest.ApiVersion is { } api && (api < OLDEST || api > VERSION)
            ? $"{manifest.Name} {manifest.Version} was built for plugin API {api}{(manifest.BuiltAgainst is { } built ? $" (turbo-cloud {built})" : "")}; this server has plugin API {VERSION} (it loads {OLDEST} to {VERSION}). Install a build made for it."
            : null;
}
