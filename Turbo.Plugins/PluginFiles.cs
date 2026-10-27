using System;
using System.IO;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

/// <summary>A plugin's own folder, read only (<see cref="IPluginFiles"/>).</summary>
internal sealed class PluginFiles(string folder) : IPluginFiles
{
    private readonly string _root = Path.GetFullPath(folder);

    public Stream? OpenRead(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path));

        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"{path} leaves the plugin's folder", nameof(path));

        return File.Exists(full) ? File.OpenRead(full) : null;
    }
}
