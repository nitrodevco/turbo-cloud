using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins.Compilation;

/// <summary>
/// The C# files a plugin is compiled from: those its manifest's <see cref="PluginManifest.Sources"/>
/// globs match in its folder (<c>server/**/*.cs</c> when it names none). A glob is relative to
/// the folder, with <c>/</c> between folders: <c>*</c> and <c>?</c> match within a name, <c>**</c>
/// any number of folders. <c>bin/</c> and <c>obj/</c> are a local build's, never sources.
/// </summary>
internal static class PluginSourceFiles
{
    public const string DEFAULT_GLOB = "server/**/*.cs";

    /// <summary>
    /// The plugin's source files, relative to <paramref name="folder"/> with <c>/</c>, in ordinal
    /// order (the order they are compiled and hashed in). None: a data plugin.
    /// </summary>
    public static IReadOnlyList<string> Find(string folder, PluginManifest manifest)
    {
        var globs = manifest.Sources is { Count: > 0 } sources ? sources : [DEFAULT_GLOB];

        foreach (var glob in globs)
        {
            if (Problem(glob) is { } problem)
                throw new ArgumentException(
                    $"The manifest's Sources glob \"{glob}\" {problem}.",
                    nameof(manifest)
                );
        }

        if (!Directory.Exists(folder))
            return [];

        return
        [
            .. Directory
                .EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(folder, file).Replace('\\', '/'))
                .Where(file => !file.Split('/').SkipLast(1).Any(IsBuildOutput))
                .Where(file => globs.Any(glob => Matches(glob, file)))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>What is wrong with <paramref name="glob"/>; null when it is a glob inside the folder.</summary>
    private static string? Problem(string glob) =>
        string.IsNullOrWhiteSpace(glob) ? "is empty"
        : glob.Contains('\\') ? "uses '\\': write folders with '/'"
        : Path.IsPathRooted(glob) || glob.StartsWith('/') ? "is not relative to the plugin's folder"
        : glob.Split('/').Any(part => part is ".." or ".") ? "leaves the plugin's folder"
        : null;

    private static bool IsBuildOutput(string folder) =>
        folder.Equals("bin", StringComparison.OrdinalIgnoreCase)
        || folder.Equals("obj", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="glob"/> matches the relative <paramref name="path"/>.</summary>
    internal static bool Matches(string glob, string path) =>
        Matches(glob.Split('/'), 0, path.Split('/'), 0);

    private static bool Matches(string[] glob, int g, string[] path, int p)
    {
        while (g < glob.Length)
        {
            if (glob[g] == "**")
            {
                // Zero or more folders, never the file's own name.
                for (var skip = p; skip < path.Length; skip++)
                {
                    if (Matches(glob, g + 1, path, skip))
                        return true;
                }

                return false;
            }

            if (
                p >= path.Length
                || !FileSystemName.MatchesSimpleExpression(glob[g], path[p], ignoreCase: true)
            )
                return false;

            g++;
            p++;
        }

        return p == path.Length;
    }
}
