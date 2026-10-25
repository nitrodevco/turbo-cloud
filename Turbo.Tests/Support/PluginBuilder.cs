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
    /// What a plugin compiled in a test builds against: every assembly loaded, and the host's
    /// contracts, its message handlers, the options extensions, plugin databases and furni logic,
    /// which a test run alone may not have loaded yet. Their paths are taken at once: the list of
    /// loaded assemblies also holds any plugin another test is unloading, and reading the
    /// references takes long enough under load to keep that plugin's context from going.
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
            typeof(Turbo.Messages.Registry.MessageContext),
            typeof(Turbo.Pipeline.EnvelopeInvokerFactory<>),
            typeof(Microsoft.Extensions.Configuration.BinderOptions),
            typeof(Turbo.Database.Context.PluginDbContextBase<>),
            typeof(Microsoft.EntityFrameworkCore.DbContext),
            typeof(System.ComponentModel.DataAnnotations.Schema.TableAttribute),
            typeof(Turbo.Rooms.Object.Logic.Furniture.Floor.FurnitureFloorLogic),
        ];

        string[] paths =
        [
            .. AppDomain
                .CurrentDomain.GetAssemblies()
                .Concat(host.Select(x => x.Assembly))
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location)
                .Distinct(),
        ];

        return paths.Select(x => MetadataReference.CreateFromFile(x));
    }

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
