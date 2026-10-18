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
    /// 2: plugin settings on the Settings page, and admin endpoints and pages (<c>Admin.IPluginAdmin</c>).
    /// 3: asset movements: consumers (<c>Turbo.Primitives.Movements.IAssetMovementConsumer</c>, registered
    /// with the host's <c>IAssetMovementConsumerRegistry</c>) and reversals (<c>IAssetReversalService</c>);
    /// <c>docs/movements.md</c>.
    /// </summary>
    public const int VERSION = 3;

    /// <summary>
    /// The oldest plugin API this server still loads: <see cref="VERSION"/> only added to it. One
    /// thing changed under it: the wallet's and the inventory's grain methods that move an asset
    /// now take a <c>MovementCause</c>, so a plugin built before them that calls one must be rebuilt
    /// (<c>docs/movements.md</c> "Plugins").
    /// </summary>
    public const int OLDEST = 1;

    /// <summary>Why the server can't take a plugin built for <paramref name="manifest"/>'s API; null when it can.</summary>
    public static string? Refusal(PluginManifest manifest) =>
        manifest.ApiVersion is { } api && (api < OLDEST || api > VERSION)
            ? $"{manifest.Name} {manifest.Version} was built for plugin API {api}{(manifest.BuiltAgainst is { } built ? $" (turbo-cloud {built})" : "")}; this server has plugin API {VERSION} (it loads {OLDEST} to {VERSION}). Install a build made for it."
            : null;
}
