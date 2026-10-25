using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Data;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Catalog;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The external variables and texts a plugin brought stay its own while the hotel's value is still
/// the one it gave: the client's files carry them only while the plugin is loaded (a theme turned
/// off, uninstalled with its data kept, or failing to start never points clients at files it no
/// longer serves), and they follow the plugin's value when it brings another. Whatever the hotel
/// changed, or had of its own, is the hotel's: always sent, never touched.
/// </summary>
public sealed class PluginOwnedGamedataTests : IDisposable
{
    private const string OVERRIDES = "asset.bundles.templates.overrides";
    private const string OVERRIDES_VALUE =
        """{"habbo-room-ui-com":"/gamedata/plugins/theme/habbo-room-ui-com.nitro"}""";

    private static readonly PlayerId STAFF = new(7);

    private readonly CatalogFixture _catalog = new();
    private readonly SettingsHarness _settings;
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-owned-" + Guid.NewGuid().ToString("N")
    );
    private readonly GamedataFileService _files;
    private readonly GamedataVariableService _variables;
    private readonly GamedataTextService _texts;
    private readonly PluginManager _manager;
    private readonly PluginAdministration _plugins;
    private readonly CapturingLogger<GamedataFileService> _filesLog = new();

    public PluginOwnedGamedataTests()
    {
        Directory.CreateDirectory(_folder);

        _settings = new SettingsHarness(_catalog.Db, "{}", (_, _) => { });

        var config = Options.Create(new GamedataConfig());
        var writes = new GamedataWriteLock();
        var loaded = new LoadedPluginGamedata();

        _files = new GamedataFileService(
            _catalog.Db,
            config,
            new FurnitureOfferCatalog(_catalog.NormalProvider(), _catalog.BuildersClubProvider()),
            _settings.Settings,
            new Fakes().Create<IAssetBundleStore>(),
            TimeProvider.System,
            loaded,
            _filesLog
        );
        _variables = new GamedataVariableService(
            _catalog.Db,
            config,
            _files,
            writes,
            _settings.Settings,
            new HabboGamedataClient(
                new Fakes().Create<IHttpClientFactory>(),
                NullLogger<HabboGamedataClient>.Instance
            ),
            NullLogger<GamedataVariableService>.Instance
        );
        _texts = new GamedataTextService(
            _catalog.Db,
            config,
            _files,
            writes,
            TimeProvider.System,
            new HotelTextProvider(_catalog.Db, config, TimeProvider.System),
            NullLogger<GamedataTextService>.Instance
        );

        var modules = new ClientModuleRegistry(
            new TestOptionsMonitor<GamedataConfig>(new GamedataConfig()),
            NullLogger<ClientModuleRegistry>.Instance
        );
        var seeds = new PluginGamedataSeeds(
            _catalog.Db,
            _variables,
            _texts,
            _files,
            loaded,
            modules,
            NullLogger<PluginGamedataSeeds>.Instance
        );

        _manager = new PluginManager(
            new ServiceCollection()
                .AddSingleton<IPluginGamedataSeeds>(seeds)
                .AddSingleton<IClientModuleDirectory>(modules)
                .BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder, UploadEnabled = true }),
            NullLogger<PluginManager>.Instance
        );
        _plugins = new PluginAdministration(
            _manager,
            new PluginTables(_catalog.Db, NullLogger<PluginTables>.Instance),
            [seeds],
            NullLogger<PluginAdministration>.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _settings.Dispose();
        _catalog.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private void WriteTheme(string variables, string texts, string source = SOURCE)
    {
        PluginBuilder.Write(_folder, "theme", source);
        WriteGamedata(variables, texts);
    }

    private void WriteGamedata(string variables, string texts)
    {
        var gamedata = Path.Combine(_folder, "theme", "gamedata");

        Directory.CreateDirectory(gamedata);
        File.WriteAllText(Path.Combine(gamedata, "external_variables.json"), variables);
        File.WriteAllText(Path.Combine(gamedata, "external_texts.txt"), texts);
    }

    /// <summary>The variables file a client would load now, as it reads it.</summary>
    private async Task<JsonObject> SentVariablesAsync()
    {
        var current = await _files.GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, Ct);

        return JsonNode
            .Parse(Encoding.UTF8.GetString(GamedataBytes.Decompress(current.Gzipped)))!
            .AsObject();
    }

    /// <summary>The texts file a client would load now, as it reads it.</summary>
    private async Task<Dictionary<string, string>> SentTextsAsync()
    {
        var current = await _files.GetCurrentAsync(GamedataFiles.EXTERNAL_TEXTS, Ct);

        return ExternalTextsFile.Parse(
            Encoding.UTF8.GetString(GamedataBytes.Decompress(current.Gzipped))
        );
    }

    private const string THEME_VARIABLES = $$"""
        {
            "{{OVERRIDES}}": {{OVERRIDES_VALUE}},
            "theme.color": "#ffcc00",
            "shared.key": "plugin"
        }
        """;

    [Fact]
    public async Task Its_own_are_sent_only_while_it_is_loaded_and_the_hotels_changes_always()
    {
        await _variables.SaveAsync("shared.key", "\"hotel\"", STAFF, Ct);
        WriteTheme(THEME_VARIABLES, "theme.title=Theme\ntheme.body=Body\n");

        await _manager.LoadAllAsync(ct: Ct);
        // Staff made one of its variables and one of its texts their own.
        await _variables.SaveAsync("theme.color", "\"#000000\"", STAFF, Ct);
        await _texts.SaveAsync("theme.body", "Our body", STAFF, Ct);

        var sent = await SentVariablesAsync();
        sent[OVERRIDES]!.ToJsonString().Should().Be(OVERRIDES_VALUE);
        sent["theme.color"]!.GetValue<string>().Should().Be("#000000");
        sent["shared.key"]!.GetValue<string>().Should().Be("hotel");
        (await SentTextsAsync()).Should().Contain("theme.title", "Theme");

        var off = (await _plugins.SetEnabledAsync("theme", false, Ct))!;

        off.Ok.Should().BeTrue(off.Message);
        off.Message.Should().Contain("not sent the external variables and texts it brought");
        sent = await SentVariablesAsync();
        sent.ContainsKey(OVERRIDES).Should().BeFalse("its files are no longer served");
        sent["theme.color"]!.GetValue<string>().Should().Be("#000000", "staff changed it");
        sent["shared.key"]!.GetValue<string>().Should().Be("hotel");
        var texts = await SentTextsAsync();
        texts.Should().NotContainKey("theme.title");
        texts.Should().Contain("theme.body", "Our body");

        (await _plugins.SetEnabledAsync("theme", true, Ct))!.Ok.Should().BeTrue();
        (await SentVariablesAsync()).ContainsKey(OVERRIDES).Should().BeTrue();
        (await SentTextsAsync()).Should().Contain("theme.title", "Theme");

        var uninstalled = (
            await _plugins.UninstallAsync("theme", PluginUninstallMode.KeepData, STAFF, Ct)
        )!;

        uninstalled.Ok.Should().BeTrue(uninstalled.Message);
        uninstalled.Message.Should().Contain("not sent the external variables and texts");
        (await SentVariablesAsync()).ContainsKey(OVERRIDES).Should().BeFalse();
        (await SentTextsAsync()).Should().NotContainKey("theme.title");
        // Kept: installing it again sends them again.
        (await _texts.GetValuesAsync(["theme.title"], Ct))
            .Should()
            .ContainKey("theme.title");

        WriteTheme(THEME_VARIABLES, "theme.title=Theme\ntheme.body=Body\n");
        await _manager.LoadAllAsync(ct: Ct);

        (await SentVariablesAsync())[OVERRIDES]!.ToJsonString().Should().Be(OVERRIDES_VALUE);
        (await SentTextsAsync()).Should().Contain("theme.title", "Theme");

        await _manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public async Task A_plugin_that_fails_to_start_has_its_own_withheld()
    {
        WriteTheme(THEME_VARIABLES, "theme.title=Theme\n", FAILING);

        await _manager.LoadAllAsync(ct: Ct);

        _manager.Snapshot().Single().State.Should().Be(PluginStateType.Failed);
        (await _variables.GetValuesAsync(["theme.color"], Ct)).Should().ContainKey("theme.color");
        var sent = await SentVariablesAsync();
        sent.ContainsKey(OVERRIDES).Should().BeFalse();
        sent.ContainsKey("theme.color").Should().BeFalse();
        (await SentTextsAsync()).Should().NotContainKey("theme.title");
    }

    [Fact]
    public async Task Its_own_follow_its_new_values_and_the_hotels_changes_stay()
    {
        WriteTheme(
            """{ "theme.a": 1, "theme.b": 1, "theme.gone": 1 }""",
            "theme.title=Old\ntheme.body=Old\n"
        );
        await _manager.LoadAllAsync(ct: Ct);
        await _variables.SaveAsync("theme.b", "5", STAFF, Ct);
        await _texts.SaveAsync("theme.body", "Ours", STAFF, Ct);

        // Its new version gives other values, and theme.gone no more.
        WriteGamedata(
            """{ "theme.a": 2, "theme.b": 2, "theme.new": 1 }""",
            "theme.title=New\ntheme.body=New\n"
        );
        await _manager.ReloadAsync("theme", Ct);

        (await _variables.GetValuesAsync(["theme.a", "theme.b", "theme.gone", "theme.new"], Ct))
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["theme.a"] = "2",
                    ["theme.b"] = "5",
                    ["theme.new"] = "1",
                }
            );
        (await _texts.GetValuesAsync(["theme.title", "theme.body"], Ct))
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string> { ["theme.title"] = "New", ["theme.body"] = "Ours" }
            );

        await using (var db = await _catalog.Db.CreateDbContextAsync(Ct))
            (
                await db
                    .GamedataChangeSets.Where(x => x.Summary.Contains("updated"))
                    .Select(x => new { x.Summary, x.PlayerEntityId })
                    .ToListAsync(Ct)
            )
                .Should()
                .BeEquivalentTo([
                    new
                    {
                        Summary = "The plugin theme updated its variables",
                        PlayerEntityId = (int?)null,
                    },
                    new
                    {
                        Summary = "The plugin theme updated its texts",
                        PlayerEntityId = (int?)null,
                    },
                ]);

        // Staff's value stays theirs: sent while it is off, never moved again.
        await _plugins.SetEnabledAsync("theme", false, Ct);

        var sent = await SentVariablesAsync();
        sent["theme.b"]!.GetValue<int>().Should().Be(5);
        sent.ContainsKey("theme.a").Should().BeFalse();
        sent.ContainsKey("theme.new").Should().BeFalse();
    }

    [Fact]
    public async Task A_key_already_holding_its_value_becomes_its_own()
    {
        // A hotel that ran the plugin before what it gave was kept.
        await _variables.SaveAsync("theme.color", "\"#ffcc00\"", STAFF, Ct);
        WriteTheme(THEME_VARIABLES, string.Empty);

        await _manager.LoadAllAsync(ct: Ct);
        (await SentVariablesAsync()).ContainsKey("theme.color").Should().BeTrue();

        await _plugins.SetEnabledAsync("theme", false, Ct);

        (await SentVariablesAsync()).ContainsKey("theme.color").Should().BeFalse();
    }

    [Fact]
    public async Task Each_plugin_adds_its_own_libraries_and_the_hotels_entries_win()
    {
        WriteTheme(
            """{ "asset.bundles.templates.overrides": { "lib-a": "/theme/a", "lib-b": "/theme/b" } }""",
            string.Empty
        );
        WritePlugin(
            "view",
            """{ "asset.bundles.templates.overrides": "{\"lib-b\":\"/view/b\",\"lib-c\":\"/view/c\"}" }"""
        );

        await _manager.LoadAllAsync(ct: Ct);

        (await SentOverridesAsync())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["lib-a"] = "/theme/a",
                    ["lib-b"] = "/theme/b",
                    ["lib-c"] = "/view/c",
                },
                "each plugin's libraries are sent, the first plugin's where two name one"
            );
        (await _variables.GetValuesAsync([OVERRIDES], Ct))
            .Should()
            .BeEmpty("the hotel is given none of it");

        // Staff point one library elsewhere and take another away.
        await _variables.SaveAsync(OVERRIDES, """{"lib-a":"/hotel/a","lib-c":""}""", STAFF, Ct);

        (await SentOverridesAsync())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["lib-a"] = "/hotel/a",
                    ["lib-b"] = "/theme/b",
                    ["lib-c"] = "",
                }
            );

        // A plugin turned off takes its libraries with it; the hotel's stay.
        (await _plugins.SetEnabledAsync("theme", false, Ct))!
            .Ok.Should()
            .BeTrue();

        (await SentOverridesAsync())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["lib-a"] = "/hotel/a",
                    ["lib-b"] = "/view/b",
                    ["lib-c"] = "",
                }
            );

        (await _plugins.SetEnabledAsync("view", false, Ct))!.Ok.Should().BeTrue();

        (await SentVariablesAsync())[OVERRIDES]!
            .ToJsonString()
            .Should()
            .Be("""{"lib-a":"/hotel/a","lib-c":""}""", "the hotel's own value, as it is");

        await _manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public async Task A_plugin_that_gave_the_hotel_its_libraries_before_takes_them_back()
    {
        // As a hotel that ran the theme before its libraries were merged: the variable is the
        // plugin's own, still as it gave it.
        await _variables.SaveAsync(OVERRIDES, OVERRIDES_VALUE, STAFF, Ct);
        await using (var db = await _catalog.Db.CreateDbContextAsync(Ct))
        {
            db.GamedataPluginValues.Add(
                new Turbo.Database.Entities.Gamedata.GamedataPluginValueEntity
                {
                    PluginKey = "theme",
                    RecordType = Turbo.Primitives.Gamedata.Enums.GamedataRecordType.Variable,
                    Key = OVERRIDES,
                    Value = OVERRIDES_VALUE,
                }
            );
            await db.SaveChangesAsync(Ct);
        }
        WriteTheme(THEME_VARIABLES, string.Empty);

        await _manager.LoadAllAsync(ct: Ct);

        (await _variables.GetValuesAsync([OVERRIDES], Ct)).Should().BeEmpty();
        (await SentVariablesAsync())[OVERRIDES]!.ToJsonString().Should().Be(OVERRIDES_VALUE);

        WritePlugin("view", """{ "asset.bundles.templates.overrides": { "lib-c": "/view/c" } }""");
        await _manager.LoadAllAsync(ct: Ct);

        (await SentOverridesAsync()).Should().ContainKeys("habbo-room-ui-com", "lib-c");

        await _manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public async Task A_plugins_window_patch_file_is_listed_for_clients_under_its_key_while_it_is_loaded()
    {
        WriteTheme("{}", string.Empty);
        WritePlugin("view", "{}");
        WritePatches("theme");
        WritePatches("view");

        await _manager.LoadAllAsync(ct: Ct);

        var sent = await SentPatchesAsync();
        sent.Keys.Should().BeEquivalentTo(["theme", "view"], "each plugin's file, by its own key");
        sent["theme"]
            .Should()
            .MatchRegex(
                @"^/gamedata/plugins/theme/template-patches\.json\?v=[0-9a-f]{16}$",
                "served from its wwwroot by hash"
            );
        (await _variables.GetValuesAsync([ExternalVariablesFile.TEMPLATE_PATCHES], Ct))
            .Should()
            .BeEmpty("the hotel is given none of it");

        // Staff take one plugin's patches away; turning the other off takes its own with it.
        await _variables.SaveAsync(
            ExternalVariablesFile.TEMPLATE_PATCHES,
            """{"view":""}""",
            STAFF,
            Ct
        );
        (await _plugins.SetEnabledAsync("theme", false, Ct))!.Ok.Should().BeTrue();

        (await SentPatchesAsync())
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["view"] = "" });

        await _manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public async Task A_plugin_without_a_patch_file_adds_no_entry()
    {
        WriteTheme("{}", string.Empty);

        await _manager.LoadAllAsync(ct: Ct);

        (await SentVariablesAsync())
            .ContainsKey(ExternalVariablesFile.TEMPLATE_PATCHES)
            .Should()
            .BeFalse();

        await _manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public async Task Two_plugins_overriding_one_library_is_logged_naming_both_and_the_one_used()
    {
        WriteTheme(
            """{ "asset.bundles.templates.overrides": { "lib-a": "/theme/a", "lib-b": "/theme/b" } }""",
            string.Empty
        );
        WritePlugin("view", """{ "asset.bundles.templates.overrides": { "lib-b": "/view/b" } }""");

        await _manager.LoadAllAsync(ct: Ct);
        await SentVariablesAsync();

        _filesLog
            .AtLeast(Microsoft.Extensions.Logging.LogLevel.Warning)
            .Select(x => x.Message)
            .Should()
            .Contain(x =>
                x.Contains("theme, view") && x.Contains("lib-b") && x.Contains("theme's is used")
            )
            .And.NotContain(x => x.Contains("lib-a"));

        await _manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public async Task Each_plugin_adds_its_own_frame_styles_and_the_hotels_entries_win()
    {
        WriteTheme(
            """{ "frame.overrides": { "wired-setup": { "variant": "102", "color": "#336699" } } }""",
            string.Empty
        );
        WritePlugin(
            "view",
            """{ "frame.overrides": { "wired-setup": { "color": "#000000" }, "navigator": { "color": "#aa0000" } } }"""
        );

        await _manager.LoadAllAsync(ct: Ct);

        var sent = (await SentVariablesAsync())[ExternalVariablesFile.FRAME_OVERRIDES]!.AsObject();
        sent.Select(x => x.Key).Should().BeEquivalentTo(["wired-setup", "navigator"]);
        sent["wired-setup"]!["variant"]!
            .GetValue<string>()
            .Should()
            .Be("102", "the first plugin's entry is used whole");
        (await _variables.GetValuesAsync([ExternalVariablesFile.FRAME_OVERRIDES], Ct))
            .Should()
            .BeEmpty("the hotel is given none of it");

        // Staff restyle one window; turning the theme off takes its entry with it, so the other
        // plugin's is used.
        await _variables.SaveAsync(
            ExternalVariablesFile.FRAME_OVERRIDES,
            """{"navigator":{"color":"#00aa00"}}""",
            STAFF,
            Ct
        );
        (await _plugins.SetEnabledAsync("theme", false, Ct))!.Ok.Should().BeTrue();

        (await SentVariablesAsync())[ExternalVariablesFile.FRAME_OVERRIDES]!
            .ToJsonString()
            .Should()
            .Be("""{"wired-setup":{"color":"#000000"},"navigator":{"color":"#00aa00"}}""");

        await _manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public async Task A_plugins_bitmap_overlay_names_a_file_of_its_wwwroot_and_clients_get_its_address()
    {
        WriteTheme(
            """{ "asset.overlays": { "habbo-window-manager-com/theme-habbo-frame-frame-default": "art/frame.png", "habbo-room-ui-com/roomtools_gear": "/shared/gear.png", "habbo-toolbar-com/icon": "missing.png" } }""",
            string.Empty
        );

        var art = Path.Combine(_folder, "theme", "wwwroot", "art");

        Directory.CreateDirectory(art);
        File.WriteAllBytes(Path.Combine(art, "frame.png"), [1, 2, 3]);

        await _manager.LoadAllAsync(ct: Ct);

        var sent = (await SentVariablesAsync())[ExternalVariablesFile.ASSET_OVERLAYS]!
            .AsObject()
            .ToDictionary(x => x.Key, x => x.Value!.GetValue<string>());

        sent.Keys.Should()
            .BeEquivalentTo(
                [
                    "habbo-window-manager-com/theme-habbo-frame-frame-default",
                    "habbo-room-ui-com/roomtools_gear",
                ],
                "an overlay whose file is not there is left out"
            );
        sent["habbo-window-manager-com/theme-habbo-frame-frame-default"]
            .Should()
            .MatchRegex(
                @"^/gamedata/plugins/theme/art/frame\.png\?v=[0-9a-f]{16}$",
                "served from its wwwroot by hash"
            );
        sent["habbo-room-ui-com/roomtools_gear"].Should().Be("/shared/gear.png", "a url is kept");

        await _manager.UnloadAllAsync(Ct);
    }

    private void WritePatches(string key)
    {
        var wwwroot = Path.Combine(_folder, key, "wwwroot");

        Directory.CreateDirectory(wwwroot);
        File.WriteAllText(
            Path.Combine(wwwroot, PluginGamedataSeeds.TEMPLATE_PATCHES_FILE),
            """{ "patches": [ { "template": "habbo-friend-list-com/friend_list_xml", "ops": [ { "element": "", "set": { "color": 4294954240 } } ] } ] }"""
        );
    }

    /// <summary>The window patch files a client would load now, by plugin key.</summary>
    private async Task<Dictionary<string, string>> SentPatchesAsync() =>
        (await SentVariablesAsync())[ExternalVariablesFile.TEMPLATE_PATCHES]!
            .AsObject()
            .ToDictionary(x => x.Key, x => x.Value!.GetValue<string>());

    private void WritePlugin(string key, string variables)
    {
        PluginBuilder.Write(
            _folder,
            key,
            SOURCE.Replace("ThemePlugin", key + "Plugin").Replace("\"theme\"", $"\"{key}\"")
        );

        var gamedata = Path.Combine(_folder, key, "gamedata");

        Directory.CreateDirectory(gamedata);
        File.WriteAllText(Path.Combine(gamedata, "external_variables.json"), variables);
    }

    /// <summary>The template library overrides a client would read now.</summary>
    private async Task<Dictionary<string, string>> SentOverridesAsync() =>
        (await SentVariablesAsync())[OVERRIDES]!
            .AsObject()
            .ToDictionary(x => x.Key, x => x.Value!.GetValue<string>());

    private const string SOURCE = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;

        public sealed class ThemePlugin : ITurboPlugin
        {
            public string Key => "theme";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
            public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        """;

    private const string FAILING = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;

        public sealed class ThemePlugin : ITurboPlugin
        {
            public string Key => "theme";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
            public Task StartAsync(IServiceProvider services, CancellationToken ct) =>
                throw new InvalidOperationException("The theme can't start.");
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        """;
}
