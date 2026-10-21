using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Main.Settings;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Plugins.Data;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Settings;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// A plugin's settings on the admin panel's Settings page, over the real plugin manager, real
/// plugin assemblies and the real server settings: a config class the plugin binds to its section
/// is listed as the plugin's while it is loaded, a secret of it is never read back, a value saved
/// reaches the plugin at once, and the section goes with the plugin. A section the server or
/// another plugin already has is refused.
/// </summary>
public sealed class PluginSettingsTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-settings-" + Guid.NewGuid().ToString("N")
    );
    private readonly SqliteDb _db = new();
    private readonly SettingsHarness _settings;
    private readonly PluginManager _manager;

    public PluginSettingsTests()
    {
        Directory.CreateDirectory(_folder);
        _settings = new SettingsHarness(
            _db,
            """{ "Sample": { "Plugin": { "Greeting": "from-the-file" } } }""",
            (services, configuration) =>
                services.Configure<TestHotelConfig>(
                    configuration.GetSection(TestHotelConfig.SECTION_NAME)
                )
        );
        _manager = new PluginManager(
            new ServiceCollection()
                .AddSingleton(_settings.Configuration)
                .AddSingleton<IPluginSettingSections>(_settings.Settings)
                .BuildServiceProvider(),
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _manager.UnloadAllAsync().GetAwaiter().GetResult();
        _settings.Dispose();
        _db.Dispose();

        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task a_plugins_settings_are_listed_while_it_is_loaded_and_a_save_reaches_it_at_once()
    {
        PluginBuilder.Write(_folder, "sample", Source("sample", "Sample:Plugin"));
        await _manager.LoadAllAsync(ct: Ct);

        State("sample").Should().Be(PluginStateType.Loaded);

        var greeting = _settings.Settings.Get("Sample:Plugin:Greeting")!;

        greeting.Plugin.Should().Be("sample");
        greeting.Live.Should().BeTrue();
        greeting.Section.Should().Be("Sample:Plugin");
        greeting.Value.Should().Be("\"from-the-file\"");
        greeting.Default.Should().Be("\"hello\"");

        var webhook = _settings.Settings.Get("Sample:Plugin:WebhookUrl")!;

        webhook.Secret.Should().BeTrue();
        webhook.IsSet.Should().BeFalse();

        await _settings.Settings.SaveAsync("Sample:Plugin:Greeting", "\"hi\"", STAFF, Ct);
        await _settings.Settings.SaveAsync(
            "Sample:Plugin:WebhookUrl",
            "\"https://discord.test/hook\"",
            STAFF,
            Ct
        );

        // The plugin reads its options through the monitor: what it runs with is the new value.
        greeting = _settings.Settings.Get("Sample:Plugin:Greeting")!;
        greeting.Value.Should().Be("\"hi\"");
        greeting.Running.Should().Be("\"hi\"");
        greeting.PendingRestart.Should().BeFalse();

        webhook = _settings.Settings.Get("Sample:Plugin:WebhookUrl")!;
        webhook.IsSet.Should().BeTrue();
        webhook.Value.Should().BeNull();
        webhook.Running.Should().BeNull();
        _settings.Settings.GetValue("Sample:Plugin:WebhookUrl").Should().BeNull();
        (await _settings.Settings.HistoryAsync(0, Ct))
            .Items.Should()
            .Contain(x => x.Path == "Sample:Plugin:WebhookUrl" && x.Secret && x.After == null);

        // The host's own settings are the host's.
        _settings.Settings.Get("Test:Hotel:MaxUsers")!.Plugin.Should().BeNull();
    }

    [Fact]
    public async Task a_plugins_settings_go_with_it_and_come_back_when_it_loads_again()
    {
        PluginBuilder.Write(_folder, "sample", Source("sample", "Sample:Plugin"));
        await _manager.LoadAllAsync(ct: Ct);
        await _settings.Settings.SaveAsync("Sample:Plugin:Greeting", "\"hi\"", STAFF, Ct);

        Directory.Move(Path.Combine(_folder, "sample"), Path.Combine(_folder, ".away"));
        await _manager.ReloadAsync("sample", Ct);

        _settings.Settings.Get("Sample:Plugin:Greeting").Should().BeNull();
        _settings.Settings.List().Should().NotContain(x => x.Plugin == "sample");

        var save = () =>
            _settings.Settings.SaveAsync("Sample:Plugin:Greeting", "\"again\"", STAFF, Ct);

        await save.Should().ThrowAsync<ArgumentException>();

        Directory.Move(Path.Combine(_folder, ".away"), Path.Combine(_folder, "sample"));
        await _manager.ReloadAsync("sample", Ct);

        // Its override was kept: it applies again as soon as the plugin is back.
        _settings.Settings.Get("Sample:Plugin:Greeting")!.Running.Should().Be("\"hi\"");
    }

    [Fact]
    public async Task a_plugin_whose_section_is_the_servers_or_another_plugins_is_refused()
    {
        PluginBuilder.Write(_folder, "sample", Source("sample", "Sample:Plugin"));
        PluginBuilder.Write(_folder, "copycat", Source("copycat", "Sample:Plugin"));
        PluginBuilder.Write(_folder, "hotel", Source("hotel", TestHotelConfig.SECTION_NAME));
        await _manager.LoadAllAsync(ct: Ct);

        var states = _manager.Snapshot().ToDictionary(x => x.Key);

        // Loaded in the order found, so one of the two sharing a section has it.
        var (owner, refused) =
            states["sample"].State == PluginStateType.Loaded
                ? ("sample", "copycat")
                : ("copycat", "sample");

        states[owner].State.Should().Be(PluginStateType.Loaded);
        states[refused].State.Should().Be(PluginStateType.Failed);
        states[refused]
            .Error.Should()
            .Contain(
                $"The plugin {refused}'s settings Sample:Plugin are the plugin {owner}'s already"
            );
        states["hotel"].State.Should().Be(PluginStateType.Failed);
        states["hotel"].Error.Should().Contain("are the server's already");

        _settings.Settings.Get("Sample:Plugin:Greeting")!.Plugin.Should().Be(owner);
        _settings.Settings.Get("Test:Hotel:MaxUsers")!.Plugin.Should().BeNull();
    }

    [Fact]
    public async Task uninstalling_a_plugin_with_its_data_puts_its_panel_settings_back_and_keeping_it_does_not()
    {
        PluginBuilder.Write(_folder, "sample", Source("sample", "Sample:Plugin"));
        PluginBuilder.Write(_folder, "kept", Source("kept", "Kept:Plugin"));
        await _manager.LoadAllAsync(ct: Ct);

        await _settings.Settings.SaveAsync("Sample:Plugin:Greeting", "\"hi\"", STAFF, Ct);
        await _settings.Settings.SaveAsync(
            "Sample:Plugin:WebhookUrl",
            "\"https://discord.test/hook\"",
            STAFF,
            Ct
        );
        await _settings.Settings.SaveAsync("Kept:Plugin:Greeting", "\"stay\"", STAFF, Ct);

        var plugins = new PluginAdministration(
            _manager,
            new PluginTables(_db, NullLogger<PluginTables>.Instance),
            [new PluginSettingData(_settings.Settings)],
            NullLogger<PluginAdministration>.Instance
        );

        // The dry run shows a setting's value, never a secret's.
        (await plugins.PlanUninstallAsync("sample", Ct))!
            .Data.Select(x => (x.Kind, x.Name, x.Detail))
            .Should()
            .Equal(
                (PluginDataKind.Setting, "Sample:Plugin:Greeting", "\"hi\""),
                (PluginDataKind.Setting, "Sample:Plugin:WebhookUrl", null)
            );

        int saved;

        await using (var before = await _db.CreateDbContextAsync(Ct))
            saved = before.ServerSettingChanges.Count();

        (await plugins.UninstallAsync("sample", PluginUninstallMode.DeleteData, STAFF, Ct))!
            .Ok.Should()
            .BeTrue();
        (await plugins.UninstallAsync("kept", PluginUninstallMode.KeepData, STAFF, Ct))!
            .Ok.Should()
            .BeTrue();

        await using var db = await _db.CreateDbContextAsync(Ct);

        db.ServerSettings.Select(x => x.Path).Should().Equal("Kept:Plugin:Greeting");

        // Each written to the history as put back, a secret's value left out.
        var history = db.ServerSettingChanges.OrderBy(x => x.Id).Skip(saved).ToList();

        history
            .Select(x => (x.Path, x.Before, x.After, x.Secret, x.PlayerEntityId))
            .Should()
            .Equal(
                ("Sample:Plugin:Greeting", "\"hi\"", (string?)null, false, (int?)STAFF.Value),
                ("Sample:Plugin:WebhookUrl", null, null, true, (int?)STAFF.Value)
            );
    }

    private PluginStateType State(string key) =>
        _manager.Snapshot().Single(x => x.Key == key).State;

    private static string Source(string key, string section) =>
        $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Turbo.Contracts.Plugins;
            using Turbo.Contracts.Plugins.Exports;
            using Turbo.Primitives.Settings;

            public sealed class SampleConfig
            {
                public const string SECTION_NAME = "{{section}}";

                public string Greeting { get; init; } = "hello";

                [SecretSetting]
                public string WebhookUrl { get; init; } = "";
            }

            public sealed class SamplePlugin : ITurboPlugin
            {
                public string Key => "{{key}}";
                public string Version => "1.0.0";
                public void ConfigureServices(IServiceCollection services, PluginManifest manifest) =>
                    services.AddOptions<SampleConfig>().BindConfiguration(SampleConfig.SECTION_NAME);
                public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
                public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
            """;
}
