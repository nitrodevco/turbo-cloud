using System.IO;
using System.Reflection;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Serialization;
using Turbo.Plugins;
using Turbo.Plugins.Compilation;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The server runs a plugin's code only as it compiles it from the plugin's sources
/// (docs/plugin-loading.md): over the real plugin manager and a plugin folder on disk, a plugin
/// compiles and loads, an executable file in its folder or a banned API in its code is refused
/// (<see cref="PluginStateType.Rejected"/>, not tried again until its files change), a compile
/// error fails it with its file and line, a compile is kept and its copy checked, and a changed
/// source is a new version.
/// </summary>
[Collection("Plugin unload")]
public sealed class SourcePluginTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-source-plugins-" + Guid.NewGuid().ToString("N")
    );

    private readonly CapturingLogger<PluginManager> _log = new(keepExceptions: true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// A plugin's symbols carry its sources: an error thrown in its code names the plugin's own
    /// file and line, as the panel and the log show it.
    /// </summary>
    [Fact]
    public async Task A_plugin_compiles_from_its_sources_and_its_stack_traces_name_its_files()
    {
        PluginBuilder.Write(
            _folder,
            "sample",
            Plugin("throw new InvalidOperationException(\"no\");")
        );

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();

        status.State.Should().Be(PluginStateType.Failed);
        _log.Entries.Select(e => e.Exception)
            .OfType<InvalidOperationException>()
            .Single()
            .StackTrace.Should()
            .Contain($"Plugin.cs:line {START_LINE}");
    }

    [Theory]
    [InlineData("Sample.dll", "MZ", "server/Sample.dll is a .dll file")]
    [InlineData("icon.png", null, "server/icon.png is a Windows executable or .NET assembly")]
    public async Task A_plugin_folder_with_an_executable_file_is_refused_until_it_is_gone(
        string file,
        string? content,
        string reason
    )
    {
        PluginBuilder.Write(_folder, "sample", Plugin());

        var binary = Path.Combine(_folder, "sample", "server", file);

        // A real assembly, renamed: found by its first bytes.
        if (content is null)
            File.Copy(typeof(SourcePluginTests).Assembly.Location, binary);
        else
            File.WriteAllText(binary, content);

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Rejected);
        status.Error.Should().Contain(reason);

        // Not tried again while nothing changed; tried once the file is gone.
        await manager.LoadChangedAsync(Ct);
        _log.Entries.Count(e => e.Message.StartsWith("Refused sample", StringComparison.Ordinal))
            .Should()
            .Be(1);

        File.Delete(binary);
        await manager.LoadChangedAsync(Ct);

        manager.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);
        await manager.UnloadAllAsync(Ct);
    }

    /// <summary>
    /// Code the manifest's Sources do not reach would load as a data plugin, Loaded with none of
    /// its commands or handlers: the server refuses it, saying where the code is.
    /// </summary>
    [Theory]
    [InlineData(
        "src/Commands/Plugin.cs",
        "no sources found: the manifest's Sources (server/**/*.cs) match no .cs files, but 1 .cs file is under src/Commands/; move them under server/ or set Sources"
    )]
    [InlineData(
        "server/Sample.csproj",
        "no sources found: the manifest's Sources (server/**/*.cs) match no .cs files, but the folder holds server/Sample.csproj; put the plugin's .cs files under server/ or set Sources"
    )]
    public async Task A_plugin_whose_code_the_manifests_sources_miss_is_refused_not_loaded_as_data(
        string file,
        string reason
    )
    {
        PluginBuilder.Write(_folder, "sample", Array.Empty<string>());
        var path = Path.Combine(_folder, "sample", file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            file.EndsWith(".cs", StringComparison.Ordinal) ? Plugin() : "<Project />",
            Ct
        );

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Rejected);
        status.Error.Should().Be($"Refused: {reason}");
    }

    /// <summary>A data plugin has no code: a local build's output (bin/, obj/) is not code, and it loads.</summary>
    [Fact]
    public async Task A_data_plugin_loads_with_a_local_builds_output_in_its_folder()
    {
        PluginBuilder.Write(_folder, "sample", Array.Empty<string>());
        var plugin = Path.Combine(_folder, "sample");
        Directory.CreateDirectory(Path.Combine(plugin, "obj"));
        await File.WriteAllTextAsync(
            Path.Combine(plugin, "obj", "Sample.AssemblyInfo.cs"),
            "// generated",
            Ct
        );
        await File.WriteAllTextAsync(
            Path.Combine(plugin, "obj", "Sample.csproj.nuget.g.props"),
            "<Project />",
            Ct
        );

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        manager.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);
        await manager.UnloadAllAsync(Ct);
    }

    [Theory]
    [InlineData("TPP001", "System.Reflection.Assembly.Load(new byte[0]);")]
    [InlineData("TPP001", "_ = new System.Runtime.Loader.AssemblyLoadContext(\"mine\");")]
    [InlineData("TPP002", "_ = System.Runtime.InteropServices.NativeLibrary.Load(\"libc\");")]
    [InlineData("TPP003", "_ = System.Runtime.CompilerServices.Unsafe.SizeOf<int>();")]
    [InlineData("TPP004", "_ = new System.Reflection.Emit.DynamicMethod(\"m\", null, null);")]
    [InlineData("TPP005", "System.Diagnostics.Process.Start(\"cmd\");")]
    [InlineData("TPP006", "_ = typeof(Microsoft.CodeAnalysis.SyntaxTree);")]
    public async Task Code_using_what_plugins_may_not_is_refused_with_the_rule_it_breaks(
        string rule,
        string statement
    )
    {
        PluginBuilder.Write(_folder, "sample", Plugin(statement));

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Rejected);
        status.Error.Should().Contain($"server/Plugin.cs({START_LINE},").And.Contain(rule);
    }

    /// <summary>Native code and unsafe code, which are declarations rather than calls.</summary>
    [Theory]
    [InlineData(
        "TPP002",
        "[System.Runtime.InteropServices.DllImport(\"libc\")] static extern int getpid();"
    )]
    [InlineData("TPP003", "static unsafe int Read(int* p) => *p;")]
    public async Task A_declaration_plugins_may_not_make_is_refused_with_its_rule(
        string rule,
        string member
    )
    {
        PluginBuilder.Write(
            _folder,
            "sample",
            [Plugin(), $"public static class Native {{ {member} }}"]
        );

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Rejected);
        status.Error.Should().Contain("server/Plugin2.cs(1,").And.Contain(rule);
    }

    /// <summary>
    /// A plugin builds on the contracts, not on the server's internals: a room grain is not
    /// something it can name.
    /// </summary>
    [Fact]
    public async Task A_compile_error_fails_the_plugin_with_its_file_line_and_column()
    {
        PluginBuilder.Write(
            _folder,
            "sample",
            [
                Plugin(),
                "public static class Internals { public static System.Type T => typeof(Turbo.Rooms.Grains.RoomGrain); }",
            ]
        );

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Failed);
        status.Error.Should().Contain("server/Plugin2.cs(1,").And.Contain("CS0234");
    }

    [Fact]
    public async Task A_compile_is_kept_and_a_restart_loads_it_without_compiling_again()
    {
        PluginBuilder.Write(_folder, "sample", Plugin());

        var first = Manager();

        await first.LoadAllAsync(ct: Ct);
        await first.UnloadAllAsync(Ct);

        var restarted = Manager();

        await restarted.LoadAllAsync(ct: Ct);

        restarted.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);
        Compiles().Should().Be(1);
        _log.Entries.Should().Contain(e => e.Message.Contains("from the cache"));
        await restarted.UnloadAllAsync(Ct);
    }

    /// <summary>
    /// The cache's files are checked against the server's own key: an assembly put there by
    /// anyone else is deleted and the plugin compiled again from its sources.
    /// </summary>
    [Fact]
    public async Task A_changed_compiled_assembly_in_the_cache_is_compiled_again()
    {
        PluginBuilder.Write(_folder, "sample", Plugin());

        var first = Manager();

        await first.LoadAllAsync(ct: Ct);
        await first.UnloadAllAsync(Ct);

        var cached = Directory.GetFiles(
            Path.Combine(_folder, PluginCompileCache.FOLDER, "sample"),
            "*.dll"
        )[0];
        var bytes = File.ReadAllBytes(cached);

        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(cached, bytes);

        var restarted = Manager();

        await restarted.LoadAllAsync(ct: Ct);

        restarted.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);
        _log.AtLeast(LogLevel.Warning)
            .Should()
            .Contain(e => e.Message.Contains("not the one this server wrote"));
        Compiles().Should().Be(2);
        await restarted.UnloadAllAsync(Ct);
    }

    /// <summary>A changed source is a new version of the plugin, as hot reload finds it.</summary>
    [Fact]
    public async Task An_edited_source_file_is_a_new_version()
    {
        PluginBuilder.Write(_folder, "sample", Plugin(version: "1.0.0"));

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);

        PluginHelpers
            .IsCodeFile(Path.Combine(_folder, "sample", "server", "Plugin.cs"))
            .Should()
            .BeTrue();

        PluginBuilder.WriteSources(Path.Combine(_folder, "sample"), [Plugin(version: "2.0.0")]);
        await manager.LoadChangedAsync(Ct);

        manager.Snapshot().Single().State.Should().Be(PluginStateType.Loaded);
        Compiles().Should().Be(2);
        await manager.UnloadAllAsync(Ct);
    }

    /// <summary>
    /// The server's pinned generators run on a plugin's sources: an Orleans serializable type
    /// gets its codec, and a database context its model.
    /// </summary>
    [Fact]
    public async Task A_plugin_with_a_serializable_type_and_a_database_context_works()
    {
        PluginBuilder.Write(
            _folder,
            "sample",
            [
                Plugin(),
                """
                using Microsoft.EntityFrameworkCore;
                using Orleans;

                [GenerateSerializer]
                public sealed record Score([property: Id(0)] int Player, [property: Id(1)] string Name);

                public sealed class Row
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }

                public sealed class ScoresDb(DbContextOptions<ScoresDb> options) : DbContext(options)
                {
                    public DbSet<Row> Rows => Set<Row>();
                }
                """,
            ]
        );

        var manager = Manager();

        await manager.LoadAllAsync(ct: Ct);
        manager
            .Snapshot()
            .Single()
            .State.Should()
            .Be(PluginStateType.Loaded, manager.Snapshot().Single().Error);

        var plugin = AppDomain
            .CurrentDomain.GetAssemblies()
            .Last(a => a.GetName().Name == "TurboPlugin.sample");
        var score = plugin.GetType("Score")!;
        var serializer = new ServiceCollection()
            .AddSerializer(b => b.AddAssembly(plugin))
            .BuildServiceProvider()
            .GetRequiredService<Serializer>();
        var value = Activator.CreateInstance(score, 7, "Ann")!;
        var bytes = Generic(serializer, "SerializeToArray", score, value);
        var copy = Generic(serializer, "Deserialize", score, bytes);

        copy.Should().Be(value);

        var db = plugin.GetType("ScoresDb")!;
        var options = (DbContextOptionsBuilder)
            Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(db))!;

        options.UseInMemoryDatabase("scores");

        using var context = (DbContext)Activator.CreateInstance(db, options.Options)!;
        context.Model.FindEntityType(plugin.GetType("Row")!).Should().NotBeNull();

        await manager.UnloadAllAsync(Ct);
    }

    /// <summary>
    /// A plugin's project builds as the server compiles it: the server's plugin props reference
    /// exactly what the compiler does, and the template's project imports them.
    /// </summary>
    [Fact]
    public void The_plugin_props_reference_what_the_server_compiles_plugins_against()
    {
        var props = XDocument.Load(
            Path.Combine(AppContext.BaseDirectory, "plugin-sdk", "Turbo.Plugin.props")
        );

        props
            .Descendants("TurboPluginReference")
            .Select(x => x.Attribute("Include")!.Value)
            .Should()
            .Equal(PluginReferences.HOST);
        props
            .Descendants("LangVersion")
            .Single()
            .Value.Should()
            .Be("14.0", $"the server compiles plugins as {PluginCompiler.LANGUAGE}");
        File.ReadAllText(
                Path.Combine(
                    RepositoryRoot(),
                    "templates",
                    "turbo-plugin",
                    "server",
                    "Skills.csproj"
                )
            )
            .Should()
            .Contain("$(TurboBin)/plugin-sdk/Turbo.Plugin.props");
    }

    private PluginManager Manager() =>
        new(
            new ServiceCollection().BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            _log
        );

    private int Compiles() =>
        _log.Entries.Count(e => e.Message.StartsWith("Compiled plugin", StringComparison.Ordinal));

    private static object Generic(Serializer serializer, string name, Type type, object argument) =>
        typeof(Serializer)
            .GetMethods()
            .Single(m =>
                m.Name == name
                && m.IsGenericMethodDefinition
                && m.GetParameters().Select(p => p.ParameterType).ToArray() is var parameters
                && (
                    name == "Deserialize"
                        ? parameters is [{ } bytes] && bytes == typeof(byte[])
                        : parameters is [{ IsGenericParameter: true }]
                )
            )
            .MakeGenericMethod(type)
            .Invoke(serializer, [argument])!;

    private static string RepositoryRoot()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
            if (File.Exists(Path.Combine(dir.FullName, "Turbo.Cloud.sln")))
                return dir.FullName;

        throw new DirectoryNotFoundException("Turbo.Cloud.sln");
    }

    /// <summary>The line of <see cref="Plugin"/>'s start body.</summary>
    private const int START_LINE = 13;

    private static string Plugin(string start = "", string version = "1.0.0") =>
        $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Turbo.Contracts.Plugins;
            using Turbo.Contracts.Plugins.Exports;

            public sealed class SamplePlugin : ITurboPlugin
            {
                public string Key => "sample";
                public string Version => "{{version}}";
                public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
                public Task StartAsync(IServiceProvider services, CancellationToken ct) { {{start}} return Task.CompletedTask; }
                public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
            """;
}
