using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;

namespace Turbo.Tests.Support;

/// <summary>
/// A real plugin on disk, as the server finds one: <c>&lt;folder&gt;/&lt;key&gt;/manifest.json</c>
/// and its assembly, compiled here from the source given (against every assembly the tests have
/// loaded, so the host's contracts and the options extensions).
/// </summary>
internal static class PluginBuilder
{
    public static void Write(
        string pluginsFolder,
        string key,
        string source,
        int? apiVersion = null
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
                }
            )
        );
        File.WriteAllBytes(Path.Combine(folder, $"{key}.dll"), Compile(key, source));
    }

    /// <summary>
    /// What a plugin compiled in a test builds against: every assembly loaded, and the host's
    /// contracts and the options extensions, which a test run alone may not have loaded yet.
    /// </summary>
    public static IEnumerable<MetadataReference> References()
    {
        Type[] host =
        [
            typeof(ITurboPlugin),
            typeof(IServiceCollection),
            typeof(IOptionsMonitor<>),
            typeof(OptionsBuilderConfigurationExtensions),
            typeof(Turbo.Primitives.Settings.SecretSettingAttribute),
        ];

        return AppDomain
            .CurrentDomain.GetAssemblies()
            .Concat(host.Select(x => x.Assembly))
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => a.Location)
            .Distinct()
            .Select(x => MetadataReference.CreateFromFile(x));
    }

    private static byte[] Compile(string name, string source)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
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
