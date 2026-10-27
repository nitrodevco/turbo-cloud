using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Turbo.Plugins.Compilation;

/// <summary>
/// The source generators a plugin is compiled with, the server's own and pinned with it (the
/// build copies them into <c>plugin-sdk/generators/</c>): Orleans serializers
/// (<c>[GenerateSerializer]</c>), <c>[LoggerMessage]</c>, System.Text.Json's
/// <c>[JsonSerializable]</c> and <c>[GeneratedRegex]</c>. A plugin brings no generator of its own.
/// </summary>
internal static class PluginGenerators
{
    /// <summary>The generators' assemblies, by name.</summary>
    public static readonly ImmutableArray<string> FILES =
    [
        "Orleans.CodeGenerator",
        "Microsoft.Extensions.Logging.Generators",
        "System.Text.Json.SourceGeneration",
        "System.Text.RegularExpressions.Generator",
    ];

    /// <summary>
    /// What the Orleans generator reads of the build, as MSBuild would hand it over
    /// (<c>CompilerVisibleProperty</c>): a plugin's compile is a real build, not the IDE's.
    /// </summary>
    public static readonly ImmutableDictionary<string, string> BUILD_PROPERTIES =
        ImmutableDictionary.CreateRange<string, string>(
            StringComparer.Ordinal,
            [
                new KeyValuePair<string, string>("build_property.Orleans_DesignTimeBuild", "false"),
                new KeyValuePair<string, string>("build_property.Orleans_AttachDebugger", ""),
                new KeyValuePair<string, string>("build_property.Orleans_GenerateFieldIds", ""),
                new KeyValuePair<string, string>(
                    "build_property.Orleans_ConstructorAttributes",
                    ""
                ),
                new KeyValuePair<string, string>(
                    "build_property.OrleansGenerateCompatibilityInvokers",
                    ""
                ),
            ]
        );

    private static readonly Lazy<(ImmutableArray<ISourceGenerator> All, string Hash)> LOADED = new(
        Load
    );

    public static ImmutableArray<ISourceGenerator> All => LOADED.Value.All;

    /// <summary>The generators' bytes, hashed: other generators compile a plugin again.</summary>
    public static string Hash => LOADED.Value.Hash;

    private static (ImmutableArray<ISourceGenerator>, string) Load()
    {
        var folder = Path.Combine(PluginReferences.SdkFolder, "generators");
        var loader = new Loader(folder);
        var paths = FILES
            .Select(name => Path.Combine(folder, name + ".dll"))
            .Select(path =>
                File.Exists(path)
                    ? path
                    : throw new InvalidOperationException(
                        $"The server has no {path}: it was built without the plugin SDK (Turbo.Plugins/Sdk/PluginSdk.targets), so it can't compile plugins."
                    )
            )
            .ToList();
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var path in paths)
            sha.AppendData(File.ReadAllBytes(path));

        return (
            [
                .. paths.SelectMany(path =>
                    new AnalyzerFileReference(path, loader).GetGenerators(LanguageNames.CSharp)
                ),
            ],
            Convert.ToHexString(sha.GetHashAndReset())
        );
    }

    /// <summary>
    /// The generators in a context of their own, which they never leave: the compiler they run in
    /// is the server's.
    /// </summary>
    private sealed class Loader(string folder) : IAnalyzerAssemblyLoader
    {
        private readonly GeneratorContext _context = new(folder);

        public void AddDependencyLocation(string fullPath) { }

        public Assembly LoadFromPath(string fullPath) => _context.LoadFromAssemblyPath(fullPath);
    }

    private sealed class GeneratorContext(string folder)
        : AssemblyLoadContext("TurboPluginGenerators")
    {
        protected override Assembly? Load(AssemblyName name)
        {
            // Microsoft.CodeAnalysis and the framework: the server's own.
            var path = Path.Combine(folder, name.Name + ".dll");

            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }
    }
}
