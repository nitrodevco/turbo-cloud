using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Turbo.Contracts.Plugins;

namespace Turbo.Tests.Support;

/// <summary>
/// A real plugin on disk, as the server finds one: <c>&lt;folder&gt;/&lt;key&gt;/manifest.json</c>
/// and its assembly, compiled here from the source given (against every assembly the test host
/// may load, so the host's contracts and everything a server plugin can use).
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

    /// <summary>A plugin compiled from several files, as a plugin project's are.</summary>
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
                    AssemblyFile = $"{key}.dll",
                    ApiVersion = apiVersion ?? PluginApi.VERSION,
                    BuiltAgainst = "abc1234",
                    Dependencies = dependsOn.Select(k => new { Key = k }).ToArray(),
                }
            )
        );
        File.WriteAllBytes(Path.Combine(folder, $"{key}.dll"), Compile(key, sources));
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
