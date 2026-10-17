namespace Turbo.Contracts.Plugins;

/// <summary>
/// The version of what plugins build against (these contracts and the host services they are
/// handed). It goes up when a change breaks plugins built before it; a plugin declares the one it
/// was built for in its manifest's <see cref="PluginManifest.ApiVersion"/>.
/// </summary>
public static class PluginApi
{
    public const int VERSION = 1;
}
