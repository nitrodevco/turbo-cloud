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

        // The plugin class registers neither: the module is built from client/src/index.tsx, the
        // settings class is [PluginSettings].
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
                "wired effect whisper_room",
                "wired trigger credits_earned",
                "wired condition on_furni",
                "wired selector lit_furni",
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
                "policy registration",
                "policy avatar-effect",
                "policy purchase-fulfilment",
                "policy public-room-order busiest-first",
                "policy room-arrival",
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
            .BeEquivalentTo([
                "skill_stove",
                "wf_act_whisper_room",
                "wf_trg_credits_earned",
                "wf_cnd_on_furni",
                "wf_slc_lit_furni",
            ]);
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
                ("registration", "RegistrationPolicy"),
                ("avatar-effect", "AvatarEffectPolicy"),
                ("purchase-fulfilment", "PurchaseFulfilmentPolicy"),
                ("public-room-order", "BusiestFirstPolicy"),
                ("room-arrival", "RoomArrivalPolicy"),
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
        (await File.ReadAllTextAsync(Path.Combine(source, "server", "Skills.csproj"), Ct))
            .Should()
            .NotContain(
                "Turbo.Rooms.dll",
                "furni logic and wired boxes are built against the contracts alone"
            );
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
    /// An event handler, and a behaviour with its order, builds against the server assemblies the
    /// template's project references and the shared frameworks alone: the contracts it needs are
    /// in <c>Turbo.Primitives</c>. They were in <c>Turbo.Events</c> and <c>Turbo.Pipeline</c>,
    /// which every plugin with a handler referenced (docs/extensions.md, "Public means contract").
    /// </summary>
    [Fact]
    public void An_event_handler_builds_against_only_the_server_assemblies_the_template_references() =>
        ErrorsAgainstTheTemplateReferences(
                """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Turbo.Events.Registry;
                using Turbo.Pipeline.Attributes;
                using Turbo.Primitives.Rooms.Events;

                public sealed class EnteredHandler : IEventHandler<RoomEnteredEvent>
                {
                    public ValueTask HandleAsync(RoomEnteredEvent evt, EventContext ctx, CancellationToken ct) =>
                        ValueTask.CompletedTask;
                }

                [Order(-1)]
                public sealed class EnteredBehavior : IEventBehavior<RoomEnteredEvent>
                {
                    public ValueTask InvokeAsync(RoomEnteredEvent evt, EventContext ctx, Func<ValueTask> next, CancellationToken ct) =>
                        next();
                }
                """
            )
            .Should()
            .BeEmpty();

    /// <summary>
    /// A packet handler and its middleware, which answers through its <c>MessageContext</c> and
    /// publishes a plugin notice, builds against the template's references alone: the handler
    /// contracts are in <c>Turbo.Primitives</c> (they were in <c>Turbo.Messages</c>) and the host's
    /// event system is reached through <c>IEventPublisher</c> (it was <c>EventSystem</c>, in
    /// <c>Turbo.Events</c>).
    /// </summary>
    [Fact]
    public void A_packet_handler_that_publishes_builds_against_only_the_server_assemblies_the_template_references() =>
        ErrorsAgainstTheTemplateReferences(
                """
                using System;
                using System.Threading;
                using System.Threading.Tasks;
                using Turbo.Messages.Registry;
                using Turbo.Pipeline.Attributes;
                using Turbo.Primitives.Events;
                using Turbo.Primitives.Networking;
                using Turbo.Primitives.Plugins.Events;

                public sealed record PingMessage : IMessageEvent;

                public sealed record PongMessage : IComposer;

                public sealed class PingHandler(IEventPublisher events) : IMessageHandler<PingMessage>
                {
                    public async ValueTask HandleAsync(PingMessage message, MessageContext ctx, CancellationToken ct)
                    {
                        await ctx.SendComposerAsync(new PongMessage(), ct);
                        await events.PublishAsync(
                            new PluginNoticeEvent { Source = "skills", Topic = "ping", Title = $"Ping from {ctx.PlayerId}" },
                            ct
                        );
                    }
                }

                [Order(-1)]
                public sealed class PingBehavior : IMessageBehavior<PingMessage>
                {
                    public ValueTask InvokeAsync(PingMessage message, MessageContext ctx, Func<ValueTask> next, CancellationToken ct) =>
                        next();
                }
                """
            )
            .Should()
            .BeEmpty();

    /// <summary>
    /// The errors compiling <paramref name="source"/> as a plugin gives against the server
    /// assemblies the template's project references and the shared frameworks alone.
    /// </summary>
    private static List<string> ErrorsAgainstTheTemplateReferences(string source)
    {
        var server = XDocument
            .Load(Path.Combine(Template, "server", "Skills.csproj"))
            .Descendants("Reference")
            .Select(r =>
                Path.Combine(
                    AppContext.BaseDirectory,
                    r.Attribute("Include")!
                        .Value.Replace("$(TurboBin)/", "", StringComparison.Ordinal)
                )
            );
        var frameworks = new[]
        {
            typeof(object).Assembly.Location,
            typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly.Location,
        }.Select(Path.GetDirectoryName);
        var shared = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => frameworks.Contains(Path.GetDirectoryName(path)));
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "Skills",
            [
                Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                    source,
                    cancellationToken: Ct
                ),
            ],
            server
                .Concat(shared)
                .Select(path => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path)),
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary
            )
        );

        shared.Should().NotBeEmpty();

        return
        [
            .. compilation
                .GetDiagnostics(Ct)
                .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                .Select(d => d.ToString()),
        ];
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

    /// <summary>
    /// The doc comments <c>npm run make</c> writes are XML, as the plugin's documentation file
    /// (the Settings page's summaries) needs: the migration recipe's context once had
    /// <c>&lt;Name&gt;</c> in its summary, warning CS1570 on every build (stranger test S1).
    /// </summary>
    [Fact]
    public async Task Every_doc_comment_make_writes_is_well_formed_xml()
    {
        var source = Path.Combine(_folder, "source");

        CopyTemplate(source);

        foreach (
            var recipe in new[]
            {
                "command hug",
                "message ping",
                "logic skill_stove",
                "listener entered",
                "setting rate",
                "route rares/values",
                "migration AddPrizes",
                "schedule weekly-rotation",
                "policy purchase club-discount",
                "policy room-entry",
                "policy placement",
                "policy registration",
                "policy avatar-effect",
                "policy purchase-fulfilment",
                "policy public-room-order busiest-first",
                "policy room-arrival",
            }
        )
            await MakeAsync(source, recipe);

        var offenders = new List<string>();

        foreach (var file in ServerFiles(source))
        {
            var doc = new List<string>();

            foreach (var line in File.ReadLines(file).Append(string.Empty))
            {
                if (line.TrimStart().StartsWith("///", StringComparison.Ordinal))
                {
                    doc.Add(line.TrimStart()[3..]);

                    continue;
                }

                if (doc.Count > 0)
                {
                    try
                    {
                        XElement.Parse($"<doc>{string.Join('\n', doc)}</doc>");
                    }
                    catch (System.Xml.XmlException ex)
                    {
                        offenders.Add($"{Path.GetRelativePath(source, file)}: {ex.Message}");
                    }
                }

                doc.Clear();
            }
        }

        offenders.Should().BeEmpty();
    }

    /// <summary>
    /// A server built before the plugin's code (a <c>TurboBin</c> older than the template, as a
    /// checkout's bin built before the scheduler was) is named as the cause, with what to rebuild,
    /// before anything builds: by <c>tools/host.mjs</c>, <c>npm run build</c> and
    /// <c>make migration</c>, which once said only that dotnet ef failed (stranger test S1, point 1).
    /// </summary>
    [Fact]
    public async Task A_server_older_than_the_plugin_is_named_as_the_cause_before_anything_builds()
    {
        var source = Path.Combine(_folder, "source");
        var stale = Path.Combine(_folder, "stale-bin");

        CopyTemplate(source);
        Directory.CreateDirectory(stale);

        // Every Turbo namespace the template uses but the scheduler's, in a Turbo.Contracts.dll
        // beside the server's deps file (make migration reads its EF Core version there).
        var namespaces = ServerFiles(source)
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(source, "tests"),
                    "*.cs",
                    SearchOption.AllDirectories
                )
            )
            .SelectMany(File.ReadLines)
            .Select(line =>
                System.Text.RegularExpressions.Regex.Match(line, @"^using (Turbo(\.\w+)+);")
            )
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Where(ns => ns != "Turbo.Contracts.Plugins.Scheduling")
            .Distinct();
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "Turbo.Contracts",
            [
                Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                    string.Concat(
                        namespaces.Select(ns => $"namespace {ns} {{ public class Old {{ }} }}\n")
                    ),
                    cancellationToken: Ct
                ),
            ],
            [
                Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(
                    typeof(object).Assembly.Location
                ),
            ],
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary
            )
        );

        using (var dll = File.Create(Path.Combine(stale, "Turbo.Contracts.dll")))
            compilation.Emit(dll, cancellationToken: Ct).Success.Should().BeTrue();

        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Turbo.Tests.deps.json"),
            Path.Combine(stale, "Turbo.Main.deps.json")
        );

        foreach (
            var command in new[]
            {
                "tools/host.mjs",
                "tools/dev.mjs build",
                "tools/make.mjs migration AddPrizes",
            }
        )
        {
            var (exit, output) = await NodeAsync(source, command, stale);

            exit.Should().Be(1, command);
            output
                .Should()
                .Contain("is older than this plugin", command)
                .And.Contain("Turbo.Contracts.Plugins.Scheduling", command)
                .And.Contain("SkillsWeeklyReset.cs", command)
                .And.NotContain("dotnet-ef", command);
        }

        (await NodeAsync(source, "tools/host.mjs", AppContext.BaseDirectory))
            .Exit.Should()
            .Be(0, "the server these tests run has every namespace the template uses");
    }

    /// <summary>
    /// <c>npm run clean-sample</c> takes the template's sample out (stranger test S1 deleted 11 files
    /// by hand): the plugin left loads with what <c>npm run make</c> adds and nothing of the sample.
    /// </summary>
    [Fact]
    public async Task Clean_sample_leaves_a_plugin_that_loads_with_only_what_make_adds()
    {
        var source = Path.Combine(_folder, "source");
        var plugins = Path.Combine(_folder, "plugins");
        var key = TemplateKey();

        CopyTemplate(source);
        (await NodeAsync(source, "tools/clean-sample.mjs", AppContext.BaseDirectory))
            .Exit.Should()
            .Be(0);

        foreach (var recipe in new[] { "command hug", "setting rate --default 5", "window news" })
            await MakeAsync(source, recipe);

        // The README describes the sample, and tools/ names it to take it out.
        Directory
            .EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(source, f))
            .Where(f => !IsBuildOutput(f) && !f.StartsWith("tools", StringComparison.Ordinal))
            .Where(f => f != "README.md" && Path.GetExtension(f) != ".png")
            .Where(f =>
                new[]
                {
                    "SkillBook",
                    "skillStore",
                    "WeeklyReset",
                    $"{key}.window.",
                    $"{key}.state",
                }.Any(name =>
                    File.ReadAllText(Path.Combine(source, f))
                        .Contains(name, StringComparison.Ordinal)
                )
            )
            .Should()
            .BeEmpty("the sample's skill book, messages, task, window and texts are gone");

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
            .Equal("HugCommand");
        host.Messages.Messages.Should().BeEmpty();
        host.Schedules.Keys.Should().BeEmpty();

        await manager.UnloadAllAsync(Ct);
    }

    private static IEnumerable<string> ServerFiles(string plugin) =>
        Directory
            .EnumerateFiles(Path.Combine(plugin, "server"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(Path.GetRelativePath(plugin, f)));

    private static async Task<(int Exit, string Output)> NodeAsync(
        string plugin,
        string command,
        string turboBin
    )
    {
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = plugin,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var part in command.Split(' '))
            start.ArgumentList.Add(part);

        start.Environment["TurboBin"] = turboBin;

        using var node = Process.Start(start)!;
        var output = node.StandardOutput.ReadToEndAsync(Ct);
        var errors = node.StandardError.ReadToEndAsync(Ct);

        await node.WaitForExitAsync(Ct);

        return (node.ExitCode, await output + await errors);
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
        // Its client sources as they are: the server builds its module from them.
        foreach (var sub in new[] { "wwwroot", "gamedata", "client/src" })
        foreach (
            var file in Directory.EnumerateFiles(
                Path.Combine(plugin, sub),
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            var to = Path.Combine(
                folder,
                sub,
                Path.GetRelativePath(Path.Combine(plugin, sub), file)
            );

            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }
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
