using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Turbo.Plugins.Compilation;

/// <summary>
/// What a plugin compiles against, the one list: the framework's reference assemblies (.NET and
/// ASP.NET Core, Microsoft.Extensions.* with them), which the server ships in
/// <c>plugin-sdk/ref/</c>, and <see cref="HOST"/>, the server's own assemblies and packages a
/// plugin builds on. Nothing else: not the server's internals (rooms, grains, sessions), not a
/// package of the plugin's own. A plugin's project references the same through
/// <c>plugin-sdk/Turbo.Plugin.props</c>, so its local build is the server's compile.
/// It says what a plugin may build on, not what it can reach: plugins are full trust.
/// </summary>
internal static class PluginReferences
{
    /// <summary>The server's assemblies and packages a plugin builds on, by assembly name.</summary>
    public static readonly ImmutableArray<string> HOST =
    [
        // The plugin API: contracts, the hotel API and the types they use.
        "Turbo.Contracts",
        "Turbo.Primitives",
        // A plugin's own tables: PluginDbContextBase, TablePrefixProvider (docs/database.md).
        "Turbo.Database",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Abstractions",
        "Microsoft.EntityFrameworkCore.Relational",
        "Pomelo.EntityFrameworkCore.MySql",
        // Serializable types ([GenerateSerializer]) and the grain interfaces the contracts name.
        "Orleans.Core.Abstractions",
        "Orleans.Serialization.Abstractions",
        "Orleans.Serialization",
        "Orleans.Core",
        // The session types the contracts name.
        "SuperSocket.Server.Abstractions",
        "SuperSocket.Connection",
        "SuperSocket.ProtoBase",
        "SuperSocket.Primitives",
    ];

    /// <summary>Where the server keeps what plugins are compiled with, beside its own assemblies.</summary>
    public static string SdkFolder => Path.Combine(AppContext.BaseDirectory, "plugin-sdk");

    private static readonly Lazy<(ImmutableArray<MetadataReference> All, string Hash)> LOADED = new(
        Load
    );

    /// <summary>Every reference, read once per process: the compiler keeps what it read of each.</summary>
    public static ImmutableArray<MetadataReference> All => LOADED.Value.All;

    /// <summary>
    /// The references' names and module ids: another when the server is built again with any
    /// change, so a plugin compiled against the old ones is compiled again.
    /// </summary>
    public static string Hash => LOADED.Value.Hash;

    private static (ImmutableArray<MetadataReference>, string) Load()
    {
        var framework = Path.Combine(SdkFolder, "ref");

        if (!Directory.Exists(framework))
            throw new InvalidOperationException(
                $"The server has no {framework}: it was built without the plugin SDK (Turbo.Plugins/Sdk/PluginSdk.targets), so it can't compile plugins."
            );

        var paths = Directory
            .EnumerateFiles(framework, "*.dll")
            .Concat(HOST.Select(HostAssembly))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hash = new StringBuilder();

        foreach (var path in paths)
            hash.Append(Path.GetFileName(path)).Append('|').Append(ModuleId(path)).Append('\n');

        return (
            [.. paths.Select(path => MetadataReference.CreateFromFile(path))],
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash.ToString())))
        );
    }

    /// <summary>The file the server runs <paramref name="name"/> from.</summary>
    private static string HostAssembly(string name)
    {
        var file = name + ".dll";
        var path =
            TrustedAssemblies.GetValueOrDefault(file)
            ?? Path.Combine(AppContext.BaseDirectory, file);

        return File.Exists(path)
            ? path
            : throw new InvalidOperationException(
                $"The server has no {file}, which plugins are compiled against."
            );
    }

    private static Dictionary<string, string> TrustedAssemblies =>
        ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase);

    private static Guid ModuleId(string path)
    {
        using var pe = new PEReader(File.OpenRead(path));
        var metadata = pe.GetMetadataReader();

        return metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    }
}
