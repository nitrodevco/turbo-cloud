using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Turbo.Plugins.Exceptions;

namespace Turbo.Plugins.ClientBuild;

/// <summary>
/// Builds a plugin's client module from its TypeScript source: the entry under its folder's
/// <c>client/src/</c> bundled by esbuild's standalone binary (beside the server as
/// <c>esbuild/esbuild(.exe)</c>, <c>ClientBuild.targets</c>) into one ES module, not minified, with
/// its source map inline, with the options of the client module SDK's bundler preset
/// (<c>clientModuleBuildOptions</c>). A module is its own sources, the SDK and the client's shared
/// libraries (<c>react</c>, <c>react/jsx-runtime</c>, <c>zustand</c>, read from the client as the
/// SDK's preset reads them): any other package, a file outside <c>client/src/</c> or one that is not
/// TypeScript (prebuilt JavaScript) fails the build, at the line that imports it. Builds are kept
/// in <paramref name="cacheFolder"/> by a hash of every source file, the SDK and the options, so an
/// unchanged module is built once. See <c>docs/client-modules.md</c>.
/// </summary>
internal sealed class PluginClientBuilder(
    string cacheFolder,
    string? esbuild = null,
    string? sdk = null
)
{
    /// <summary>The esbuild the server is shipped with; <c>ClientBuild.targets</c> pins the same.</summary>
    public const string ESBUILD_VERSION = "0.28.2";

    /// <summary>The folder, beside the plugins, their client modules are built into.</summary>
    public const string CACHE_FOLDER = ".client-builds";

    /// <summary>The folder, under a plugin's own, its client module's sources are in.</summary>
    public const string SOURCE_FOLDER = "client/src";

    /// <summary>The entry a plugin's module is built from when it registers none itself, the first there.</summary>
    public static readonly string[] CONVENTIONAL_ENTRIES =
    [
        $"{SOURCE_FOLDER}/index.tsx",
        $"{SOURCE_FOLDER}/index.ts",
    ];

    /// <summary>What a module's sources are: TypeScript, and JSON data.</summary>
    private static readonly string[] SOURCE_EXTENSIONS = [".ts", ".tsx", ".json"];

    /// <summary>The longest a build may take before it is stopped and fails.</summary>
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(60);

    private const string SDK_PACKAGE = "@nitrodevco/client-module-sdk";

    /// <summary>Each shared library and its file under the SDK folder's <c>shared/</c>.</summary>
    private static readonly string[] SHARED = ["react", "zustand"];

    private readonly string _esbuild =
        esbuild
        ?? Path.Combine(
            AppContext.BaseDirectory,
            "esbuild",
            OperatingSystem.IsWindows() ? "esbuild.exe" : "esbuild"
        );
    private readonly string _sdk = Path.GetFullPath(
        sdk ?? Path.Combine(AppContext.BaseDirectory, "client-build")
    );
    private string? _sdkHash;

    /// <summary>The first of <see cref="CONVENTIONAL_ENTRIES"/> in <paramref name="folder"/>; null when it has none.</summary>
    public static string? ConventionalEntry(string folder) =>
        CONVENTIONAL_ENTRIES.FirstOrDefault(x => File.Exists(Path.Combine(folder, x)));

    /// <summary>
    /// The module <paramref name="entry"/> (relative to <paramref name="folder"/>, under
    /// <c>client/src/</c>) builds to, from the cache when its sources are unchanged. Throws
    /// <see cref="ArgumentException"/> for an entry that is not a TypeScript file there, and
    /// <see cref="PluginClientBuildException"/> with esbuild's errors (<c>file:line:column: text</c>)
    /// when it does not build.
    /// </summary>
    public PluginClientBuild Build(string key, string folder, string entry)
    {
        folder = Path.GetFullPath(folder);
        entry = entry.Replace('\\', '/').TrimStart('/');

        var sources = Path.GetFullPath(Path.Combine(folder, SOURCE_FOLDER));

        if (
            !IsSource(sources, Path.GetFullPath(Path.Combine(folder, entry)))
            || Path.GetExtension(entry) is not (".ts" or ".tsx")
            || !File.Exists(Path.Combine(folder, entry))
        )
            throw new ArgumentException(
                $"A client module is built from a .ts or .tsx file under {SOURCE_FOLDER}/, not {entry}.",
                nameof(entry)
            );

        if (!File.Exists(_esbuild))
            throw new PluginClientBuildException(
                key,
                [
                    $"esbuild {ESBUILD_VERSION} is not at {_esbuild}. Build or publish the server again (it fetches esbuild for its platform from the npm registry, ClientBuild.targets), or put esbuild {ESBUILD_VERSION}'s binary for this platform there.",
                ]
            );

        if (!Directory.Exists(Path.Combine(_sdk, "client-module-sdk")))
            throw new PluginClientBuildException(
                key,
                [
                    $"The client module SDK the server builds with is not at {_sdk}. Build or publish the server again.",
                ]
            );

        var hash = Hash(key, folder, entry);
        var output = Path.Combine(cacheFolder, key, hash[..16]);
        var file = Path.Combine(output, $"{key}.js");

        if (File.Exists(file))
            return new PluginClientBuild(file, Reused: true, Milliseconds: 0);

        var watch = Stopwatch.StartNew();
        var temporary = Path.Combine(cacheFolder, key, $".{hash[..16]}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(temporary);

        try
        {
            var meta = Path.Combine(temporary, "meta.json");
            var built = Path.Combine(temporary, $"{key}.js");

            Run(key, folder, Arguments(entry, built, meta));

            var errors = Check(folder, sources, meta);

            if (errors.Count > 0)
                throw new PluginClientBuildException(key, errors);

            File.Delete(meta);

            try
            {
                Directory.Move(temporary, output);
            }
            catch (IOException) when (File.Exists(file))
            {
                // The same sources built at once twice: either is the build.
            }

            Prune(Path.Combine(cacheFolder, key), output);

            return new PluginClientBuild(file, Reused: false, watch.ElapsedMilliseconds);
        }
        finally
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
        }
    }

    /// <summary>
    /// esbuild's options, as <c>clientModuleBuildOptions</c> gives them for a module that is not
    /// minified; its plugin for the shared libraries is the aliases to <c>shared/</c>. Packages are
    /// left unresolved (no <c>node_modules</c> is read: <see cref="Check"/> refuses them), and the
    /// server's own empty tsconfig is the one read, so a plugin's paths map no package to a file.
    /// </summary>
    private List<string> Arguments(string entry, string outfile, string metafile)
    {
        var arguments = new List<string>
        {
            entry,
            "--bundle",
            "--format=esm",
            "--platform=browser",
            "--target=es2022",
            "--jsx=automatic",
            "--sourcemap=inline",
            "--legal-comments=none",
            "--define:process.env.NODE_ENV=\"development\"",
            "--packages=external",
            $"--tsconfig={Path.Combine(_sdk, "tsconfig.json")}",
            $"--alias:{SDK_PACKAGE}={Path.Combine(_sdk, "client-module-sdk")}",
            "--log-level=error",
            "--log-limit=0",
            "--color=false",
            $"--outfile={outfile}",
            $"--metafile={metafile}",
        };

        arguments.AddRange(SHARED.Select(x => $"--alias:{x}={Path.Combine(_sdk, "shared", x)}"));

        return arguments;
    }

    private void Run(string key, string folder, List<string> arguments)
    {
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(_esbuild);

            if (!mode.HasFlag(UnixFileMode.UserExecute))
                File.SetUnixFileMode(_esbuild, mode | UnixFileMode.UserExecute);
        }

        var start = new ProcessStartInfo(_esbuild)
        {
            WorkingDirectory = folder,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        var printed = new StringBuilder();
        using var process = new Process { StartInfo = start };

        process.ErrorDataReceived += (_, e) => Print(printed, e.Data);
        process.OutputDataReceived += (_, e) => Print(printed, e.Data);

        if (!process.Start())
            throw new PluginClientBuildException(key, [$"esbuild did not start ({_esbuild})."]);

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        if (!process.WaitForExit(TIMEOUT))
        {
            process.Kill(entireProcessTree: true);

            throw new PluginClientBuildException(
                key,
                [$"The build took longer than {TIMEOUT.TotalSeconds:0} seconds and was stopped."]
            );
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
            lock (printed)
                throw new PluginClientBuildException(key, Errors(printed.ToString()));
    }

    private static void Print(StringBuilder printed, string? line)
    {
        if (line is null)
            return;

        lock (printed)
            printed.Append(line).Append('\n');
    }

    /// <summary>
    /// esbuild's errors as <c>file:line:column: text</c> (its own line and column), one each, from
    /// what it prints: <c>✘ [ERROR] text</c>, a blank line, then <c>    file:line:column:</c>.
    /// </summary>
    internal static List<string> Errors(string printed)
    {
        var errors = new List<string>();
        string? text = null;

        void Flush(string? where)
        {
            if (text is not null)
                errors.Add(where is null ? text : $"{where}: {text}");

            text = null;
        }

        foreach (var line in printed.Split('\n').Select(x => x.TrimEnd('\r')))
        {
            var marker = line.IndexOf("[ERROR] ", StringComparison.Ordinal);

            if (marker >= 0)
            {
                Flush(null);
                text = line[(marker + "[ERROR] ".Length)..].Trim();
            }
            else if (text is not null && line.StartsWith("    ") && line.EndsWith(':'))
                Flush(line.Trim().TrimEnd(':'));
        }

        Flush(null);

        return errors.Count > 0 ? errors : [printed.Trim()];
    }

    /// <summary>
    /// What the module imports, from esbuild's metafile: every import is a source under
    /// <c>client/src/</c> or the SDK's; a package left unresolved or anything else is an error at
    /// the line that imports it.
    /// </summary>
    private List<string> Check(string folder, string sources, string metafile)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(metafile));
        var errors = new List<string>();

        foreach (var input in document.RootElement.GetProperty("inputs").EnumerateObject())
        foreach (var import in input.Value.GetProperty("imports").EnumerateArray())
        {
            var path = import.GetProperty("path").GetString() ?? string.Empty;
            var original = import.TryGetProperty("original", out var named)
                ? named.GetString() ?? path
                : path;

            if (import.TryGetProperty("external", out var external) && external.GetBoolean())
                errors.Add(
                    $"{Where(folder, input.Name, original)}: \"{original}\" is not built into a client module: a module is its own sources under {SOURCE_FOLDER}/, {SDK_PACKAGE} and the client's {string.Join(", ", SHARED)}; no other package is bundled."
                );
            else if (!IsAllowed(sources, ResolveInput(folder, path)))
                errors.Add(
                    $"{Where(folder, input.Name, original)}: \"{original}\" is not a TypeScript source under {SOURCE_FOLDER}/: a client module is built from its .ts and .tsx files (and .json data) only, no prebuilt JavaScript."
                );
        }

        return errors;
    }

    private bool IsAllowed(string sources, string? path) =>
        path is not null
        && (
            path.StartsWith(_sdk + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || IsSource(sources, path)
                && SOURCE_EXTENSIONS.Contains(
                    Path.GetExtension(path),
                    StringComparer.OrdinalIgnoreCase
                )
        );

    private static bool IsSource(string sources, string path) =>
        path.StartsWith(sources + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>A metafile path as a full one; null for what is no file (a <c>data:</c> URL, a disabled path).</summary>
    private static string? ResolveInput(string folder, string path)
    {
        if (path.Contains(':') && !Path.IsPathRooted(path))
            return null;

        try
        {
            return Path.GetFullPath(Path.Combine(folder, path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary><c>file:line:column</c> of the first <paramref name="import"/> in quotes in the file, as esbuild counts (column from 0).</summary>
    private static string Where(string folder, string file, string import)
    {
        string text;

        try
        {
            text = File.ReadAllText(Path.Combine(folder, file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return file;
        }

        var at = new[] { '\'', '"', '`' }
            .Select(quote => text.IndexOf($"{quote}{import}{quote}", StringComparison.Ordinal))
            .Where(x => x >= 0)
            .DefaultIfEmpty(-1)
            .Min();

        if (at < 0)
            return file;

        var line = 1 + text.AsSpan(0, at).Count('\n');
        var column = at - (text.LastIndexOf('\n', Math.Max(0, at - 1)) + 1);

        return $"{file}:{line}:{column}";
    }

    /// <summary>A hash of all a build reads: esbuild's version and options, the SDK, and every file under <c>client/src/</c>.</summary>
    private string Hash(string key, string folder, string entry)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        hash.AppendData(
            Encoding.UTF8.GetBytes(
                $"{ESBUILD_VERSION}\n{key}\n{string.Join('\n', Arguments(entry, "", ""))}\n{SdkHash()}\n"
            )
        );
        AppendFolder(hash, Path.Combine(folder, SOURCE_FOLDER));

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private string SdkHash()
    {
        if (_sdkHash is not null)
            return _sdkHash;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendFolder(hash, _sdk);

        return _sdkHash = Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendFolder(IncrementalHash hash, string folder)
    {
        if (!Directory.Exists(folder))
            return;

        foreach (
            var file in Directory
                .EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Select(x => Path.GetRelativePath(folder, x).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
        )
        {
            var bytes = File.ReadAllBytes(Path.Combine(folder, file));

            hash.AppendData(Encoding.UTF8.GetBytes($"{file}\n{bytes.Length}\n"));
            hash.AppendData(bytes);
        }
    }

    /// <summary>
    /// The plugin's earlier builds go once a new one is made: the one before it is kept, in case
    /// it is still served while the plugin reloads, and a build left half done an hour ago goes.
    /// </summary>
    private static void Prune(string folder, string current)
    {
        var others = new DirectoryInfo(folder)
            .EnumerateDirectories()
            .Where(x => !string.Equals(x.FullName, current, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var old = others
            .Where(x => !x.Name.StartsWith('.'))
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .Skip(1)
            .Concat(
                others.Where(x =>
                    x.Name.StartsWith('.') && x.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1)
                )
            );

        foreach (var directory in old)
        {
            try
            {
                directory.Delete(recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}

/// <summary>A module built (<see cref="PluginClientBuilder.Build"/>): its file, whether the cache had it, and how long the build took.</summary>
internal sealed record PluginClientBuild(string File, bool Reused, long Milliseconds);
