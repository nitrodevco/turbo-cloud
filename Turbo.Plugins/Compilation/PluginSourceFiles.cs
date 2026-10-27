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
    /// order (the order they are compiled and hashed in). When none match, check
    /// <see cref="UnreachedCode"/> before treating the plugin as data.
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

    /// <summary>
    /// Why a plugin whose <see cref="PluginManifest.Sources"/> match no file is not a data plugin:
    /// its folder holds code they do not reach (<c>.cs</c> files elsewhere, or a <c>.csproj</c>,
    /// outside <c>bin/</c> and <c>obj/</c>), which would otherwise load as data, none of its
    /// commands or handlers there. Null when they match a file, or when the folder holds no code.
    /// </summary>
    public static string? UnreachedCode(string folder, PluginManifest manifest)
    {
        if (!Directory.Exists(folder) || Find(folder, manifest).Count > 0)
            return null;

        var globs = manifest.Sources is { Count: > 0 } sources ? sources : [DEFAULT_GLOB];
        var listed = $"the manifest's Sources ({string.Join(", ", globs)}) match no .cs files";
        var code = CodeFiles(folder, "*.cs");

        if (code.Count > 0)
        {
            var dirs = code.Select(file =>
                    file.Contains('/') ? file[..(file.LastIndexOf('/') + 1)] : "the plugin's folder"
                )
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var files = code.Count == 1 ? "1 .cs file is" : $"{code.Count} .cs files are";

            return $"no sources found: {listed}, but {files} under {Names(dirs)}; move them under server/ or set Sources";
        }

        return CodeFiles(folder, "*.csproj") is { Count: > 0 } projects
            ? $"no sources found: {listed}, but the folder holds {Names(projects)}; put the plugin's .cs files under server/ or set Sources"
            : null;
    }

    /// <summary>
    /// The files matching <paramref name="pattern"/> (<c>*.cs</c>, <c>*.csproj</c>) anywhere in
    /// <paramref name="folder"/> but a local build's output, relative with <c>/</c>, in order.
    /// </summary>
    internal static List<string> CodeFiles(string folder, string pattern) =>
        [
            .. Directory
                .EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(folder, file).Replace('\\', '/'))
                .Where(file => !file.Split('/').SkipLast(1).Any(IsBuildOutput))
                .Order(StringComparer.Ordinal),
        ];

    private static string Names(List<string> names) =>
        names.Count <= 5
            ? string.Join(", ", names)
            : $"{string.Join(", ", names.Take(5))} and {names.Count - 5} more";

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
