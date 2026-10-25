using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Commands;
using Turbo.Database.Configuration;
using Turbo.Database.Migrations;
using Turbo.Gamedata;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Messages.PluginMessages;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.Players.Permissions;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Settings;
using Turbo.Primitives.Texts;
using Turbo.Rooms;
using Turbo.Rooms.Object.Logic;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// <c>templates/turbo-plugin</c> is what <c>dotnet new turbo-plugin</c> generates, and a template's
/// files are a working plugin as they stand (its default name is <c>Skills</c>). So its server half
/// is compiled and loaded here as a hotel would load it, with what its <c>npm run make</c> adds, and
/// what its project builds against is checked against the server's own assemblies: a change to the
/// server that breaks a new plugin fails here, not in an author's first build.
/// </summary>
public sealed class PluginTemplateTests : IDisposable
{
    private static readonly string Template = Path.Combine(
        RepositoryRoot(),
        "templates",
        "turbo-plugin"
    );

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-template-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_template_plugin_loads_registers_its_messages_and_module_and_gives_the_hotel_its_texts()
    {
        var key = TemplateKey();

        PluginBuilder.Write(_folder, key, ServerSources(Template));
        CopyPluginFiles(Template, Path.Combine(_folder, key), key);

        var host = new PluginTestHost(_db, _fakes);
        var manager = host.Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);

        // The plugin class registers neither: the module is wwwroot/<key>.js, the settings class
        // is [PluginSettings].
        host.Modules.Modules.Should()
            .ContainSingle()
            .Which.Url.Should()
            .StartWith($"/gamedata/plugins/{key}/{key}.js?v=");
        host.Modules.FindFile(key, $"{key}.png").Should().NotBeNull("the module's toolbar icon");
        _fakes
            .Log.Of(nameof(IPluginSettingSections.Add))
            .Should()
            .ContainSingle()
            .Which.Args[1]
            .As<IReadOnlyList<Type>>()
            .Select(t => t.Name)
            .Should()
            .Equal("SkillsSettings");
        host.Messages.Messages.Select(x => x.Name)
            .Should()
            .Equal($"{key}.get", $"{key}.state", $"{key}.train");
        host.Messages.Messages.Single(x => x.Name == $"{key}.state")
            .Schema.Should()
            .Be(
                """[["level","int"],["experience","int"],["needed","int"]]""",
                "client/src/skill.ts reads these"
            );
        (await host.Texts.GetValuesAsync([$"{key}.window.title"], Ct))
            .Should()
            .ContainKey($"{key}.window.title");
        host.Loaded.OwnedPartsOf(ExternalVariablesFile.TEMPLATE_PATCHES)
            .Should()
            .ContainSingle("its wwwroot/template-patches.json is listed for clients")
            .Which.Plugin.Should()
            .Be(key);

        await manager.UnloadAllAsync(Ct);

        host.Modules.Modules.Should().BeEmpty();
        host.Messages.Messages.Should().BeEmpty();
    }

    /// <summary>
    /// Each kind <c>npm run make</c> (tools/make.mjs) writes, into a copy of the template, loads
    /// with the plugin and is found by the scan that finds it on a hotel.
    /// </summary>
    [Fact]
    public async Task Every_make_recipe_writes_a_piece_the_plugin_loads_with()
    {
        var source = Path.Combine(_folder, "source");
        var plugins = Path.Combine(_folder, "plugins");
        var key = TemplateKey();

        CopyTemplate(source);

        foreach (
            var recipe in new[]
            {
                "command hug",
                "message levelled --to client --fields level:int,name:string",
                "message ping",
                "logic skill_stove",
                "listener entered",
                "setting rate --type int --default 5",
                "route rares/values --anonymous",
                "window news",
                "patch habbo-friend-list-com/friend_list_xml",
                "migration AddPrizes",
                "schedule weekly-rotation --every 01:00:00",
                "policy purchase club-discount",
                "policy room-entry",
                "policy placement",
            }
        )
            await MakeAsync(source, recipe);

        PluginBuilder.Write(plugins, key, ServerSources(source));
        CopyPluginFiles(source, Path.Combine(plugins, key), key);

        var host = new PluginTestHost(_db, _fakes);
        var manager = host.Manager(plugins);

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);

        _fakes
            .Log.On<ICommandRegistryProvider>()
            .SelectMany(c => (IEnumerable<ICommand>)c.Args[0]!)
            .Select(c => c.GetType().Name)
            .Should()
            .BeEquivalentTo("SkillsCommand", "HugCommand");
        _fakes
            .Log.On<IPermissionRegistryProvider>()
            .SelectMany(c => ((IPermissionNodeSource)c.Args[0]!).Nodes)
            .Select(n => n.Node)
            .Should()
            .Contain($"{key}.command.hug");
        _fakes
            .Log.On<IRoomObjectLogicProvider>()
            .Where(c => c.Method == nameof(IRoomObjectLogicProvider.RegisterLogic))
            .Select(c => c.Args[0])
            .Should()
            .Equal("skill_stove");
        _fakes
            .Log.On<IRoomEventListenerRegistry>()
            .SelectMany(c => (IEnumerable<IRoomEventListener>)c.Args[0]!)
            .Select(l => l.GetType().Name)
            .Should()
            .Equal("EnteredListener");
        host.Messages.Messages.Single(x => x.Name == $"{key}.levelled")
            .Schema.Should()
            .Be("""[["level","int"],["name","string"]]""");
        host.Messages.Messages.Select(x => x.Name).Should().Contain($"{key}.ping");
        host.WebRoutes.MethodsAt(key, "rares/values").Should().Equal("GET");
        host.Policies.Registered.Select(x => (x.Kind, x.Policy))
            .Should()
            .BeEquivalentTo([
                ("purchase", "ClubDiscountPolicy"),
                ("room-entry", "RoomEntryPolicy"),
                ("placement", "PlacementPolicy"),
            ]);
        host.Schedules.Keys.Should()
            .BeEquivalentTo($"{key}.SkillsWeeklyReset", $"{key}.WeeklyRotation");
        host.MigratorLog.Entries.Select(e => e.Message)
            .Should()
            .Contain(
                m =>
                    m.StartsWith("Database SkillsDb: migrations are off", StringComparison.Ordinal),
                "the context was found and its migrations asked for as the plugin loaded"
            );
        (await host.Texts.GetValuesAsync([$"{key}.news.title"], Ct))
            .Should()
            .ContainKey($"{key}.news.title");
        _fakes
            .Log.Of(nameof(IPluginSettingSections.Add))
            .Should()
            .ContainSingle()
            .Which.Args[2]
            .As<Func<Type, object?>>()
            .Invoke(
                host.SettingsType ?? throw new InvalidOperationException("no settings were listed")
            )
            .Should()
            .BeEquivalentTo(new { Rate = 5 }, o => o.ExcludingMissingMembers());
        (await File.ReadAllTextAsync(Path.Combine(source, "wwwroot", "template-patches.json"), Ct))
            .Should()
            .Contain("habbo-friend-list-com/friend_list_xml");
        (await File.ReadAllTextAsync(Path.Combine(source, "client", "src", "made", "index.ts"), Ct))
            .Should()
            .Contain("from './NewsWindow'")
            .And.Contain($"from './{key}.levelled'")
            .And.NotContain(
                $"from './{key}.ping'",
                "a message to the server has nothing to activate"
            );

        await manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public void Every_server_assembly_the_template_builds_against_is_one_the_server_ships()
    {
        var references = new[]
        {
            Path.Combine("server", "Skills.csproj"),
            Path.Combine("tests", "Skills.Tests", "Skills.Tests.csproj"),
        }
            .SelectMany(project =>
                XDocument.Load(Path.Combine(Template, project)).Descendants("Reference")
            )
            .Select(r => r.Attribute("Include")!.Value)
            .ToList();

        references.Should().NotBeEmpty();
        references
            .Select(r => r.Replace("$(TurboBin)/", "", StringComparison.Ordinal))
            .Should()
            .OnlyContain(file => File.Exists(Path.Combine(AppContext.BaseDirectory, file)));
    }

    /// <summary>
    /// The template is what a new author copies: it reaches the hotel through the hotel API
    /// (docs/hotel-api.md) and its tests through FakeHotel, never a grain or a session, which are
    /// the server's internals.
    /// </summary>
    [Fact]
    public void The_template_reaches_the_hotel_through_the_hotel_api_and_names_no_grain()
    {
        var offenders = Directory
            .EnumerateFiles(Template, "*.cs", SearchOption.AllDirectories)
            .SelectMany(file =>
                File.ReadLines(file)
                    .Select((line, index) => (file, line, index))
                    .Where(x =>
                        x.line.Contains("Grain", StringComparison.Ordinal)
                        || x.line.Contains("ISessionGateway", StringComparison.Ordinal)
                        || x.line.Contains("Turbo.Primitives.Orleans", StringComparison.Ordinal)
                    )
            )
            .Select(x =>
                $"{Path.GetRelativePath(Template, x.file)}:{x.index + 1}: {x.line.Trim()}"
            );

        offenders.Should().BeEmpty();
        File.ReadAllText(Path.Combine(Template, "server", "SkillBook.cs"))
            .Should()
            .Contain("IHotel hotel");
    }

    [Fact]
    public void The_template_names_what_dotnet_new_replaces_and_the_files_it_renames()
    {
        var template = JsonDocument
            .Parse(File.ReadAllText(Path.Combine(Template, ".template.config", "template.json")))
            .RootElement;
        var symbols = template.GetProperty("symbols");
        var content = string.Join(
            '\n',
            Directory
                .EnumerateFiles(Template, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.template.config"))
                .Where(f => Path.GetExtension(f) != ".png")
                .Select(File.ReadAllText)
        );

        template.GetProperty("shortName").GetString().Should().Be("turbo-plugin");

        foreach (var name in new[] { "pluginName", "pluginKey", "turbo", "nitro" })
            content
                .Should()
                .Contain(
                    symbols.GetProperty(name).GetProperty("replaces").GetString(),
                    $"{name} replaces text the template has"
                );

        File.Exists(Path.Combine(Template, "server", "Skills.csproj")).Should().BeTrue();
        File.Exists(Path.Combine(Template, "wwwroot", "skills.png")).Should().BeTrue();
    }

    private static string TemplateKey() =>
        JsonDocument
            .Parse(File.ReadAllText(Path.Combine(Template, "manifest.json")))
            .RootElement.GetProperty("Key")
            .GetString()!;

    /// <summary>The server half's sources, as its project compiles them.</summary>
    private static List<string> ServerSources(string plugin) =>
        [
            .. Directory
                .EnumerateFiles(Path.Combine(plugin, "server"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !IsBuildOutput(Path.GetRelativePath(plugin, f)))
                .Select(File.ReadAllText),
        ];

    /// <summary>The plugin's files beside its assembly, and the module its client half builds.</summary>
    private static void CopyPluginFiles(string plugin, string folder, string key)
    {
        foreach (var sub in new[] { "wwwroot", "gamedata" })
        foreach (var file in Directory.EnumerateFiles(Path.Combine(plugin, sub)))
        {
            Directory.CreateDirectory(Path.Combine(folder, sub));
            File.Copy(file, Path.Combine(folder, sub, Path.GetFileName(file)));
        }

        // npm run dev builds it; only its being there matters here.
        File.WriteAllText(
            Path.Combine(folder, "wwwroot", $"{key}.js"),
            "export default { activate() {} };"
        );
    }

    private static void CopyTemplate(string to)
    {
        foreach (var file in Directory.EnumerateFiles(Template, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Template, file);

            if (IsBuildOutput(relative))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(to, relative))!);
            File.Copy(file, Path.Combine(to, relative));
        }
    }

    private static bool IsBuildOutput(string relative) =>
        relative
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "bin" or "obj" or "node_modules" or "dist");

    private static async Task MakeAsync(string plugin, string recipe)
    {
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = plugin,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add(Path.Combine("tools", "make.mjs"));

        foreach (var part in recipe.Split(' '))
            start.ArgumentList.Add(part);

        // The migration recipe runs dotnet ef once it has written the context; not here. It reads
        // the server's EF Core version from the server it builds against: these tests' own.
        start.Environment["TURBO_MAKE_EF"] = "0";
        start.Environment["TurboBin"] = AppContext.BaseDirectory;

        using var node = Process.Start(start)!;
        var output = node.StandardOutput.ReadToEndAsync(Ct);
        var errors = node.StandardError.ReadToEndAsync(Ct);

        await node.WaitForExitAsync(Ct);

        node.ExitCode.Should().Be(0, $"make {recipe}: {await errors}{await output}");
    }

    private static string RepositoryRoot()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
            if (File.Exists(Path.Combine(dir.FullName, "Turbo.Cloud.sln")))
                return dir.FullName;

        throw new InvalidOperationException("Turbo.Cloud.sln was not found above the tests.");
    }
}
