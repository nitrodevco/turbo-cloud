using System.IO;
using System.Net.Http;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Data;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Settings;
using Turbo.Primitives.Texts;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin brings external variables and texts in its folder's <c>gamedata/</c>: loading it gives
/// the hotel each one it has none of, through the panel's own services and history, and never
/// touches one the hotel has. Uninstalling it with its data takes away the ones still as it gave
/// them; one staff changed since, and the hotel's own, stay.
/// </summary>
public sealed class PluginGamedataSeedTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-seed-" + Guid.NewGuid().ToString("N")
    );
    private readonly GamedataVariableService _variables;
    private readonly GamedataTextService _texts;
    private readonly PluginManager _manager;
    private readonly PluginAdministration _plugins;

    public PluginGamedataSeedTests()
    {
        Directory.CreateDirectory(_folder);

        var config = Options.Create(new GamedataConfig());
        var files = _fakes.Create<IGamedataFileService>();
        var writes = new GamedataWriteLock();

        _variables = new GamedataVariableService(
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
        _texts = new GamedataTextService(
            _db,
            config,
            files,
            writes,
            TimeProvider.System,
            _fakes.Create<IHotelTextProvider>(),
            NullLogger<GamedataTextService>.Instance
        );

        var seeds = new PluginGamedataSeeds(
            _variables,
            _texts,
            NullLogger<PluginGamedataSeeds>.Instance
        );

        _manager = new PluginManager(
            new ServiceCollection()
                .AddSingleton<IPluginGamedataSeeds>(seeds)
                .BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder, UploadEnabled = true }),
            NullLogger<PluginManager>.Instance
        );
        _plugins = new PluginAdministration(
            _manager,
            new PluginTables(_db, NullLogger<PluginTables>.Instance),
            [seeds],
            NullLogger<PluginAdministration>.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private void WriteSample()
    {
        PluginBuilder.Write(_folder, "sample", SOURCE);

        var gamedata = Path.Combine(_folder, "sample", "gamedata");

        Directory.CreateDirectory(gamedata);
        File.WriteAllText(
            Path.Combine(gamedata, "external_variables.json"),
            """
            {
                // The hotel keeps its own value of this one.
                "sample.hud.enabled": true,
                "sample.hud.color": "#ffcc00",
                "shared.key": "plugin"
            }
            """
        );
        File.WriteAllText(
            Path.Combine(gamedata, "external_texts.txt"),
            "sample.hud.title=Hello\nsample.hud.body=A small bar\nshared.text=plugin\n"
        );
    }

    private async Task<Dictionary<string, string>> VariablesAsync(params string[] keys) =>
        new(await _variables.GetValuesAsync(keys, Ct));

    private async Task<Dictionary<string, string>> TextsAsync(params string[] keys) =>
        new(await _texts.GetValuesAsync(keys, Ct));

    private async Task<int> ChangeSetsAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.GamedataChangeSets.CountAsync(Ct);
    }

    [Fact]
    public async Task Loading_gives_the_hotel_what_it_lacks_once_and_never_touches_what_it_has()
    {
        await _variables.SaveAsync("shared.key", "\"hotel\"", STAFF, Ct);
        await _texts.SaveAsync("shared.text", "hotel", STAFF, Ct);
        WriteSample();

        await _manager.LoadAllAsync(ct: Ct);

        (await VariablesAsync("sample.hud.enabled", "sample.hud.color", "shared.key"))
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["sample.hud.enabled"] = "true",
                    ["sample.hud.color"] = "\"#ffcc00\"",
                    ["shared.key"] = "\"hotel\"",
                }
            );
        (await TextsAsync("sample.hud.title", "sample.hud.body", "shared.text"))
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["sample.hud.title"] = "Hello",
                    ["sample.hud.body"] = "A small bar",
                    ["shared.text"] = "hotel",
                }
            );

        await using (var db = await _db.CreateDbContextAsync(Ct))
            (
                await db
                    .GamedataChangeSets.Where(x => x.Summary.StartsWith("The plugin sample"))
                    .Select(x => new { x.Summary, x.PlayerEntityId })
                    .ToListAsync(Ct)
            )
                .Should()
                .BeEquivalentTo([
                    new
                    {
                        Summary = "The plugin sample added its variables",
                        PlayerEntityId = (int?)null,
                    },
                    new
                    {
                        Summary = "The plugin sample added its texts",
                        PlayerEntityId = (int?)null,
                    },
                ]);

        var before = await ChangeSetsAsync();

        await _manager.ReloadAsync("sample", Ct);

        (await ChangeSetsAsync()).Should().Be(before, "a second load finds everything there");

        await _manager.UnloadAllAsync(Ct);
    }

    [Fact]
    public async Task Uninstalling_with_its_data_takes_away_only_what_is_still_as_the_plugin_gave_it()
    {
        await _variables.SaveAsync("shared.key", "\"hotel\"", STAFF, Ct);
        WriteSample();
        await _manager.LoadAllAsync(ct: Ct);
        // Staff made one of the plugin's texts their own.
        await _texts.SaveAsync("sample.hud.title", "Hi there", STAFF, Ct);

        var plan = (await _plugins.PlanUninstallAsync("sample", Ct))!;

        plan.Data.Select(x => (x.Kind, x.Name))
            .Should()
            .BeEquivalentTo([
                (PluginDataKind.Variable, "sample.hud.color"),
                (PluginDataKind.Variable, "sample.hud.enabled"),
                (PluginDataKind.Text, "sample.hud.body"),
                (PluginDataKind.Text, "shared.text"),
            ]);

        var result = (
            await _plugins.UninstallAsync("sample", PluginUninstallMode.DeleteData, STAFF, Ct)
        )!;

        result.Ok.Should().BeTrue(result.Message);
        result.Message.Should().Contain("2 variables, 2 texts");
        (await VariablesAsync("sample.hud.enabled", "sample.hud.color", "shared.key"))
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["shared.key"] = "\"hotel\"" });
        (await TextsAsync("sample.hud.title", "sample.hud.body", "shared.text"))
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["sample.hud.title"] = "Hi there" });

        await using var db = await _db.CreateDbContextAsync(Ct);

        (
            await db
                .GamedataChangeSets.Where(x => x.Summary.Contains("uninstalled"))
                .Select(x => x.PlayerEntityId)
                .ToListAsync(Ct)
        )
            .Should()
            .Equal(STAFF.Value, STAFF.Value);
    }

    [Fact]
    public async Task Keeping_its_data_leaves_what_it_gave()
    {
        WriteSample();
        await _manager.LoadAllAsync(ct: Ct);

        var result = (
            await _plugins.UninstallAsync("sample", PluginUninstallMode.KeepData, STAFF, Ct)
        )!;

        result.Ok.Should().BeTrue(result.Message);
        (await VariablesAsync("sample.hud.enabled")).Should().ContainKey("sample.hud.enabled");
        (await TextsAsync("sample.hud.title")).Should().ContainKey("sample.hud.title");
    }

    private const string SOURCE = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;

        public sealed class SamplePlugin : ITurboPlugin
        {
            public string Key => "sample";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
            public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        """;
}
