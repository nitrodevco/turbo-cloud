namespace Turbo.Primitives.ClientModules;

/// <summary>
/// A module as the host keeps it: the plugin's key and version, the folder its files are served
/// from (the plugin's <c>wwwroot/</c>, a full path) and the entry file in it.
/// </summary>
public sealed record ClientModuleSource(string Key, string Version, string Root, string Entry);
