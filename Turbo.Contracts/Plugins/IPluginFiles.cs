using System.IO;

namespace Turbo.Contracts.Plugins;

/// <summary>
/// The files a plugin ships in its own folder (a data file beside its sources: rooms to make, a
/// word list), read as the plugin runs. A plugin is compiled from its sources and has no assembly
/// to embed a resource in or to find its folder by; take this by constructor instead. Read only,
/// and only inside the plugin's folder.
/// </summary>
public interface IPluginFiles
{
    /// <summary>
    /// Opens <paramref name="path"/> (relative to the plugin's folder, <c>/</c> between folders)
    /// to read; null when there is no such file. Throws for a path that leaves the folder.
    /// </summary>
    Stream? OpenRead(string path);
}
