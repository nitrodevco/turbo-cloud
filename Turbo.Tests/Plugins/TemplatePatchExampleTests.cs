using System.IO;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Settings;
using Turbo.Primitives.Texts;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The examples in <c>docs/examples/template-patches</c> are the documentation's proof that two
/// plugins change one core window without knowing of each other, so they are built and loaded here as
/// a hotel would: the theme's patch file is listed for clients under its key without the theme naming
/// it, and the add-on's module is listed beside it.
/// </summary>
public sealed class TemplatePatchExampleTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-template-patch-example-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_theme_and_the_add_on_load_together_and_clients_are_told_of_both()
    {
        // The add-on's packet handlers are host types of Turbo.Messages and Turbo.Pipeline (implementation
        // projects, not contracts: docs/extensions.md §2.3): loaded, so the plugin compiles against them.
        _ = typeof(Turbo.Messages.Registry.MessageContext).Assembly;
        _ = typeof(Turbo.Pipeline.Registry.IHandler<,>).Assembly;

        await WriteExampleAsync("purse-theme", "PurseThemePlugin.cs");
        await WriteExampleAsync("purse-skills", "PurseSkillsPlugin.cs");

        var loaded = new LoadedPluginGamedata();
        var config = Options.Create(new GamedataConfig());
        var files = _fakes.Create<IGamedataFileService>();
        var writes = new GamedataWriteLock();
        var variables = new GamedataVariableService(
            _db,
            config,
            files,
            writes,
            _fakes.Create<IServerSettings>(),
            new HabboGamedataClient(
                _fakes.Create<IHttpClientFactory>(),
                NullLogger<HabboGamedataClient>.Instance
            ),
            NullLogger<GamedataVariableService>.Instance
        );
        var texts = new GamedataTextService(
            _db,
            config,
            files,
            writes,
            TimeProvider.System,
            _fakes.Create<IHotelTextProvider>(),
            NullLogger<GamedataTextService>.Instance
        );
        var registry = new ClientModuleRegistry(
            new TestOptionsMonitor<GamedataConfig>(new GamedataConfig()),
            NullLogger<ClientModuleRegistry>.Instance
        );
        var manager = new PluginManager(
            new ServiceCollection()
                .AddSingleton<IClientModuleDirectory>(registry)
                .AddSingleton<Turbo.Primitives.Networking.Extensions.IExtensionPacketRegistry>(
                    new Turbo.Networking.Extensions.ExtensionPacketRegistry()
                )
                .AddSingleton<IPluginGamedataSeeds>(
                    new PluginGamedataSeeds(
                        _db,
                        variables,
                        texts,
                        files,
                        loaded,
                        registry,
                        NullLogger<PluginGamedataSeeds>.Instance
                    )
                )
                .BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        await manager.LoadAllAsync(ct: Ct);

        manager
            .Snapshot()
            .Should()
            .OnlyContain(x => x.State == PluginStateType.Loaded, "both examples load")
            .And.HaveCount(2);

        // The theme has no module: its only content is the patch file, listed under its own key.
        var parts = loaded.OwnedPartsOf(ExternalVariablesFile.TEMPLATE_PATCHES);
        parts.Should().ContainSingle().Which.Plugin.Should().Be("pursetheme");
        JsonDocument
            .Parse(parts[0].Part)
            .RootElement.GetProperty("pursetheme")
            .GetString()
            .Should()
            .StartWith("/gamedata/plugins/pursetheme/template-patches.json?v=");
        JsonDocument
            .Parse(
                await File.ReadAllTextAsync(
                    Path.Combine(
                        _folder,
                        "pursetheme",
                        "wwwroot",
                        PluginGamedataSeeds.TEMPLATE_PATCHES_FILE
                    ),
                    Ct
                )
            )
            .RootElement.GetProperty("patches")[0]
            .GetProperty("template")
            .GetString()
            .Should()
            .Be("habbo-toolbar-com/purse_xml");

        registry.Modules.Should().ContainSingle().Which.Key.Should().Be("skills");

        await manager.UnloadAllAsync(Ct);

        registry.Modules.Should().BeEmpty();
        loaded.OwnedPartsOf(ExternalVariablesFile.TEMPLATE_PATCHES).Should().BeEmpty();
    }

    private async Task WriteExampleAsync(string folder, string source)
    {
        var example = Path.Combine(
            RepositoryRoot(),
            "docs",
            "examples",
            "template-patches",
            folder
        );
        var key = JsonDocument
            .Parse(await File.ReadAllTextAsync(Path.Combine(example, "manifest.json"), Ct))
            .RootElement.GetProperty("Key")
            .GetString()!;

        PluginBuilder.Write(
            _folder,
            key,
            await File.ReadAllTextAsync(Path.Combine(example, source), Ct)
        );

        foreach (var file in Directory.EnumerateFiles(Path.Combine(example, "wwwroot")))
        {
            Directory.CreateDirectory(Path.Combine(_folder, key, "wwwroot"));
            File.Copy(file, Path.Combine(_folder, key, "wwwroot", Path.GetFileName(file)));
        }
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
