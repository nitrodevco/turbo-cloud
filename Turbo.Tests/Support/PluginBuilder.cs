using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Turbo.Contracts.Plugins;

namespace Turbo.Tests.Support;

/// <summary>
/// A real plugin on disk, as the server finds one: <c>&lt;folder&gt;/&lt;key&gt;/manifest.json</c>
/// and its C# sources in <c>server/</c>, which the server compiles as it loads the plugin.
/// </summary>
internal static class PluginBuilder
{
    public static void Write(
        string pluginsFolder,
        string key,
        string source,
        int? apiVersion = null,
        params string[] dependsOn
    ) => Write(pluginsFolder, key, [source], apiVersion, dependsOn);

    /// <summary>A plugin of several files, as a plugin project's are.</summary>
    public static void Write(
        string pluginsFolder,
        string key,
        IReadOnlyList<string> sources,
        int? apiVersion = null,
        params string[] dependsOn
    )
    {
        var folder = Path.Combine(pluginsFolder, key);

        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "manifest.json"),
            JsonSerializer.Serialize(
                new
                {
                    Name = key,
                    Key = key,
                    Version = "1.0.0",
                    Author = "Staff",
                    ApiVersion = apiVersion ?? PluginApi.VERSION,
                    Dependencies = dependsOn.Select(k => new { Key = k }).ToArray(),
                }
            )
        );
        WriteSources(folder, sources);
    }

    /// <summary>
    /// <paramref name="sources"/> as the plugin's <c>server/</c> files, replacing the ones there:
    /// <c>Plugin.cs</c>, then <c>Plugin2.cs</c>...
    /// </summary>
    public static void WriteSources(string pluginFolder, IReadOnlyList<string> sources)
    {
        var server = Path.Combine(pluginFolder, "server");

        if (Directory.Exists(server))
            Directory.Delete(server, recursive: true);

        Directory.CreateDirectory(server);

        for (var i = 0; i < sources.Count; i++)
            File.WriteAllText(
                Path.Combine(server, i == 0 ? "Plugin.cs" : $"Plugin{i + 1}.cs"),
                sources[i]
            );
    }

    /// <summary>
    /// What a plugin compiled in a test builds against: every assembly the test host may load
    /// (the framework's, the server's and their packages), read once. Not the assemblies loaded
    /// so far: a class run on its own had not loaded Microsoft.Extensions.Hosting.Abstractions,
    /// so a plugin with a hosted service did not compile, and that list also holds any plugin
    /// another test is unloading, whose context reading the references kept from going.
    /// </summary>
    public static IEnumerable<MetadataReference> References() => HostReferences.Value;

    private static readonly Lazy<MetadataReference[]> HostReferences = new(() =>
        [
            .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(x => MetadataReference.CreateFromFile(x)),
        ]
    );

    /// <summary>The assembly <paramref name="source"/> compiles to, as a plugin's would.</summary>
    public static byte[] Compile(string name, string source) => Compile(name, [source]);

    /// <summary>The assembly <paramref name="sources"/> compile to, one file each.</summary>
    public static byte[] Compile(string name, IReadOnlyList<string> sources)
    {
        var compilation = CSharpCompilation.Create(
            name,
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var dll = new MemoryStream();
        var emitted = compilation.Emit(dll);

        emitted
            .Success.Should()
            .BeTrue(string.Join("; ", emitted.Diagnostics.Select(d => d.ToString())));

        return dll.ToArray();
    }
}
