using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Messages.PluginMessages;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Texts;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// <c>templates/turbo-plugin</c> is what <c>dotnet new turbo-plugin</c> generates, and a template's
/// files are a working plugin as they stand (its default name is <c>Skills</c>). So its server half
/// is compiled and loaded here as a hotel would load it, and what its project builds against is
/// checked against the server's own assemblies: a change to the server that breaks a new plugin
/// fails here, not in an author's first build.
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
        var manifest = JsonDocument
            .Parse(await File.ReadAllTextAsync(Path.Combine(Template, "manifest.json"), Ct))
            .RootElement;
        var key = manifest.GetProperty("Key").GetString()!;
        var sources = Directory
            .EnumerateFiles(Path.Combine(Template, "server"), "*.cs")
            .Select(File.ReadAllText)
            .ToList();

        PluginBuilder.Write(_folder, key, sources);

        foreach (var sub in new[] { "wwwroot", "gamedata" })
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Template, sub)))
        {
            Directory.CreateDirectory(Path.Combine(_folder, key, sub));
            File.Copy(file, Path.Combine(_folder, key, sub, Path.GetFileName(file)));
        }

        // The module the client half builds (npm run dev); only its being there matters here.
        await File.WriteAllTextAsync(
            Path.Combine(_folder, key, "wwwroot", $"{key}.js"),
            "export default { activate() {} };",
            Ct
        );

        var config = Options.Create(new GamedataConfig());
        var files = _fakes.Create<IGamedataFileService>();
        var texts = new GamedataTextService(
            _db,
            config,
            files,
            new GamedataWriteLock(),
            TimeProvider.System,
            _fakes.Create<IHotelTextProvider>(),
            NullLogger<GamedataTextService>.Instance
        );
        var modules = new ClientModuleRegistry(
            new TestOptionsMonitor<GamedataConfig>(new GamedataConfig()),
            NullLogger<ClientModuleRegistry>.Instance
        );
        var packets = new ExtensionPacketRegistry();
        var messages = new PluginMessageDirectory(packets);
        var loaded = new LoadedPluginGamedata();
        var host = new ServiceCollection()
            .AddSingleton<IClientModuleDirectory>(modules)
            .AddSingleton<IExtensionPacketRegistry>(packets)
            .AddSingleton<IPluginGamedataSeeds>(
                new PluginGamedataSeeds(
                    _db,
                    _fakes.Create<IGamedataVariableService>(),
                    texts,
                    files,
                    loaded,
                    modules,
                    NullLogger<PluginGamedataSeeds>.Instance
                )
            )
            .BuildServiceProvider();
        var manager = new PluginManager(
            host,
            new AssemblyProcessor([
                new PluginMessageFeatureProcessor(
                    new MessageRegistry(host, NullLogger<MessageRegistry>.Instance),
                    messages,
                    NullLoggerFactory.Instance,
                    host
                ),
            ]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);

        modules
            .Modules.Should()
            .ContainSingle()
            .Which.Url.Should()
            .StartWith($"/gamedata/plugins/{key}/{key}.js?v=");
        modules.FindFile(key, $"{key}.png").Should().NotBeNull("the module's toolbar icon");
        messages
            .Messages.Select(x => x.Name)
            .Should()
            .Equal($"{key}.get", $"{key}.state", $"{key}.train");
        messages
            .Messages.Single(x => x.Name == $"{key}.state")
            .Schema.Should()
            .Be(
                """[["level","int"],["experience","int"],["needed","int"]]""",
                "client/src/skill.ts reads these"
            );
        (await texts.GetValuesAsync([$"{key}.window.title"], Ct))
            .Should()
            .ContainKey($"{key}.window.title");
        loaded
            .OwnedPartsOf(ExternalVariablesFile.TEMPLATE_PATCHES)
            .Should()
            .ContainSingle("its wwwroot/template-patches.json is listed for clients")
            .Which.Plugin.Should()
            .Be(key);

        await manager.UnloadAllAsync(Ct);

        modules.Modules.Should().BeEmpty();
        messages.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Every_server_assembly_the_template_builds_against_is_one_the_server_ships()
    {
        var project = XDocument.Load(Path.Combine(Template, "server", "Skills.csproj"));
        var references = project
            .Descendants("Reference")
            .Select(r => r.Attribute("Include")!.Value)
            .ToList();

        references.Should().NotBeEmpty();
        references
            .Select(r => r.Replace("$(TurboBin)/", "", StringComparison.Ordinal))
            .Should()
            .OnlyContain(file => File.Exists(Path.Combine(AppContext.BaseDirectory, file)));
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
