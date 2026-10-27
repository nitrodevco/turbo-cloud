namespace Turbo.Primitives.ClientModules;

/// <summary>
/// A module as the host keeps it: the plugin's key and version, the folder its other files are
/// served from (the plugin's <c>wwwroot/</c>, a full path), the name the module is served under
/// beside them (<c>&lt;key&gt;.js</c>) and the file the server built it into (a full path).
/// </summary>
public sealed record ClientModuleSource(
    string Key,
    string Version,
    string Root,
    string Entry,
    string Module
);
