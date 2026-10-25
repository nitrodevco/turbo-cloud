using System;
using System.Collections.Immutable;

namespace Turbo.Primitives.ClientModules;

/// <summary>
/// The client modules the hotel has now, as the host sees them: the plugins' registrations
/// (<see cref="IClientModuleRegistry"/>, bound to each plugin's folder), the list clients are
/// sent, and the files the gamedata host serves.
/// </summary>
public interface IClientModuleDirectory
{
    /// <summary>Every module now, by key; what a client that accepted <c>client.modules</c> is sent.</summary>
    ImmutableArray<ClientModuleSnapshot> Modules { get; }

    /// <summary>Raised after a module is added or taken away.</summary>
    event System.Action? Changed;

    /// <summary>
    /// Adds a module; disposing the handle takes it away. Throws as
    /// <see cref="IClientModuleRegistry.Register"/> does.
    /// </summary>
    IDisposable Add(ClientModuleSource source);

    /// <summary>
    /// Serves the files under <paramref name="root"/> (a plugin's <c>wwwroot/</c>) as the plugin
    /// <paramref name="key"/>'s, whether or not it registers a module: a theme or data-only plugin
    /// whose variables name them. Disposing the handle stops it. Clients are sent no module for it.
    /// </summary>
    IDisposable AddFiles(string key, string root);

    /// <summary>
    /// The full path of a file a plugin serves, <paramref name="path"/> under its <c>wwwroot/</c>;
    /// null when no module or files are served under the key, the path leaves the folder, names a hidden file or folder,
    /// or there is no such file.
    /// </summary>
    string? FindFile(string key, string path);

    /// <summary>The SHA-256 of a file <see cref="FindFile"/> answered, lowercase hex; kept while the file is unchanged.</summary>
    string HashOf(string fullPath);

    /// <summary>
    /// The address clients load a file a plugin serves at (<see cref="FindFile"/>), by its hash;
    /// null when there is no such file.
    /// </summary>
    string? AddressOf(string key, string path);
}
