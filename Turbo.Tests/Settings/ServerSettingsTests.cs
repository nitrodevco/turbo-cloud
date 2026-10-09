using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Admin.Configuration;
using Turbo.Main.Settings;
using Turbo.Primitives.Players;
using Turbo.Primitives.Settings.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Settings;

/// <summary>
/// The server's settings in the admin panel: every option of every registered config class, with
/// where its value comes from; overrides above appsettings.json that apply after a restart; the
/// environment above them all; secrets replaced but never read back.
/// </summary>
public sealed class ServerSettingsTests : IDisposable
{
    private const string APP_SETTINGS = """
        {
            "Test": {
                "Hotel": {
                    "MaxUsers": 50,
                    "Layouts": [ "a", "b", "c", "d", "e" ],
                    "Prices": { "chair": 5, "lamp": 3 },
                    "ApiKey": "from-the-file"
                }
            }
        }
        """;

    private static readonly PlayerId STAFF = new(7);

    private readonly SqliteDb _db = new();
    private readonly List<SettingsHarness> _harnesses = [];
    private readonly List<string> _variables = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (var name in _variables)
            Environment.SetEnvironmentVariable(name, null);

        foreach (var harness in _harnesses)
            harness.Dispose();

        _db.Dispose();
    }

    [Fact]
    public void every_setting_is_listed_with_its_default_its_value_and_where_that_comes_from()
    {
        var settings = Harness().Settings;

        var maxUsers = settings.Get("Test:Hotel:MaxUsers")!;
        var name = settings.Get("test:hotel:name")!;

        maxUsers.Kind.Should().Be("integer");
        maxUsers.Default.Should().Be("25");
        maxUsers.Value.Should().Be("50");
        maxUsers.Source.Should().Be(ServerSettingSource.AppSettings);
        maxUsers.SourceName.Should().Be("appsettings.json");
        name.Value.Should().Be("\"Turbo\"");
        name.Source.Should().Be(ServerSettingSource.Default);
        name.Summary.Should().Be("What the hotel is called, as players see it.");
        settings.Get("Test:Hotel:Mode")!.Options.Should().Equal("Normal", "Maintenance");
        settings.Get("Test:Hotel:Links:Site")!.Value.Should().Be("\"https://hotel.example\"");
        settings.Get("Test:Hotel:Layouts")!.Kind.Should().Be("list");
        settings.Get("Test:Hotel:Prices")!.Kind.Should().Be("map");
        settings.Get("Test:Hotel:IsNamed").Should().BeNull();
    }

    [Fact]
    public async Task an_override_wins_over_the_file_and_applies_after_a_restart()
    {
        var harness = Harness();

        harness.Running<TestHotelConfig>().MaxUsers.Should().Be(50);

        var saved = await harness.Settings.SaveAsync("Test:Hotel:MaxUsers", "80", STAFF, Ct);

        saved.Value.Should().Be("80");
        saved.Running.Should().Be("50");
        saved.PendingRestart.Should().BeTrue();
        saved.Source.Should().Be(ServerSettingSource.Panel);
        harness.Running<TestHotelConfig>().MaxUsers.Should().Be(50);

        var restarted = Restart(harness);

        restarted.Running<TestHotelConfig>().MaxUsers.Should().Be(80);
        restarted.Settings.Get("Test:Hotel:MaxUsers")!.PendingRestart.Should().BeFalse();
    }

    [Fact]
    public async Task a_list_or_map_is_overridden_whole_not_merged_with_the_files()
    {
        var harness = Harness();

        // The class starts the list with "default"; the file's five items are replaced.
        await harness.Settings.SaveAsync(
            "Test:Hotel:Layouts",
            """["default", "x", "y"]""",
            STAFF,
            Ct
        );
        await harness.Settings.SaveAsync("Test:Hotel:Prices", """{ "sofa": 9 }""", STAFF, Ct);

        harness.Settings.Get("Test:Hotel:Layouts")!.Value.Should().Be("""["default","x","y"]""");

        var bound = Restart(harness).Running<TestHotelConfig>();

        bound.Layouts.Should().Equal("default", "x", "y");
        bound.Prices.Should().BeEquivalentTo(new Dictionary<string, int> { ["sofa"] = 9 });
    }

    [Fact]
    public async Task a_list_keeps_the_items_its_class_starts_with_which_configuration_cant_take_away()
    {
        var harness = Harness();

        var drop = () => harness.Settings.SaveAsync("Test:Hotel:Layouts", """["x"]""", STAFF, Ct);

        await drop.Should().ThrowAsync<ArgumentException>().WithMessage("*default items*");

        await harness.Settings.SaveAsync("Test:Hotel:Layouts", """["default"]""", STAFF, Ct);

        Restart(harness).Running<TestHotelConfig>().Layouts.Should().Equal("default");
    }

    [Fact]
    public async Task every_kind_of_value_survives_the_trip_through_the_configuration()
    {
        var harness = Harness();

        await harness.Settings.SaveAsync("Test:Hotel:Name", "\"Habbo's\"", STAFF, Ct);
        await harness.Settings.SaveAsync("Test:Hotel:Ratio", "0.25", STAFF, Ct);
        await harness.Settings.SaveAsync("Test:Hotel:Open", "false", STAFF, Ct);
        await harness.Settings.SaveAsync("Test:Hotel:Mode", "\"Maintenance\"", STAFF, Ct);
        await harness.Settings.SaveAsync("Test:Hotel:Timeout", "\"00:00:30\"", STAFF, Ct);
        await harness.Settings.SaveAsync(
            "Test:Hotel:Links:Site",
            "\"https://other.example\"",
            STAFF,
            Ct
        );

        var bound = Restart(harness).Running<TestHotelConfig>();

        bound.Name.Should().Be("Habbo's");
        bound.Ratio.Should().Be(0.25);
        bound.Open.Should().BeFalse();
        bound.Mode.Should().Be(TestHotelMode.Maintenance);
        bound.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        bound.Links.Site.Should().Be("https://other.example");
    }

    [Theory]
    [InlineData("Test:Hotel:MaxUsers", "\"many\"")]
    [InlineData("Test:Hotel:MaxUsers", "not json")]
    [InlineData("Test:Hotel:Mode", "\"Closed\"")]
    [InlineData("Test:Hotel:Open", "null")]
    [InlineData("Test:Hotel:Nothing", "1")]
    [InlineData("Test:Hotel:Port", "4000")]
    public async Task a_value_of_the_wrong_kind_an_unknown_setting_and_a_startup_one_are_refused(
        string path,
        string value
    )
    {
        var settings = Harness().Settings;

        var save = () => settings.SaveAsync(path, value, STAFF, Ct);

        await save.Should().ThrowAsync<ArgumentException>();
        (await settings.HistoryAsync(0, Ct)).Total.Should().Be(0);
    }

    [Fact]
    public async Task the_environment_wins_over_the_panel_which_says_what_sets_it_and_cant_change_it()
    {
        var harness = Harness();

        SetVariable($"{harness.EnvironmentPrefix}Test__Hotel__MaxUsers", "120");

        var restarted = Restart(harness);
        var maxUsers = restarted.Settings.Get("Test:Hotel:MaxUsers")!;

        maxUsers.Value.Should().Be("120");
        maxUsers.Source.Should().Be(ServerSettingSource.Environment);
        maxUsers.SourceName.Should().Be($"{harness.EnvironmentPrefix}Test__Hotel__MaxUsers");

        var save = () => restarted.Settings.SaveAsync("Test:Hotel:MaxUsers", "80", STAFF, Ct);

        await save.Should().ThrowAsync<ArgumentException>().WithMessage("*can't override*");
    }

    [Fact]
    public async Task an_override_saved_before_the_environment_set_the_setting_still_loses_to_it()
    {
        var harness = Harness();

        await harness.Settings.SaveAsync("Test:Hotel:MaxUsers", "80", STAFF, Ct);
        SetVariable($"{harness.EnvironmentPrefix}Test__Hotel__MaxUsers", "120");

        Restart(harness).Running<TestHotelConfig>().MaxUsers.Should().Be(120);
    }

    [Fact]
    public async Task a_secret_is_replaced_but_never_read_back_nor_kept_in_the_history()
    {
        var harness = Harness();
        var secret = harness.Settings.Get("Test:Hotel:ApiKey")!;

        secret.Secret.Should().BeTrue();
        secret.IsSet.Should().BeTrue();
        secret.Value.Should().BeNull();
        secret.Default.Should().BeNull();
        harness.Settings.GetValue("Test:Hotel:ApiKey").Should().BeNull();

        var saved = await harness.Settings.SaveAsync("Test:Hotel:ApiKey", "\"new-key\"", STAFF, Ct);

        saved.Value.Should().BeNull();
        saved.Running.Should().BeNull();
        saved.PendingRestart.Should().BeTrue();
        Restart(harness).Running<TestHotelConfig>().ApiKey.Should().Be("new-key");

        var change = (await harness.Settings.HistoryAsync(0, Ct))
            .Items.Should()
            .ContainSingle()
            .Which;

        change.Secret.Should().BeTrue();
        change.Before.Should().BeNull();
        change.After.Should().BeNull();
    }

    [Fact]
    public async Task putting_a_setting_back_lets_the_files_value_apply_and_both_changes_are_kept()
    {
        var harness = Harness();
        var before = harness.Settings.Version;

        await harness.Settings.SaveAsync("Test:Hotel:MaxUsers", "80", STAFF, Ct);

        harness.Settings.Version.Should().BeGreaterThan(before);
        (await harness.Settings.ResetAsync("Test:Hotel:MaxUsers", STAFF, Ct)).Should().BeTrue();
        (await harness.Settings.ResetAsync("Test:Hotel:MaxUsers", STAFF, Ct)).Should().BeFalse();

        var setting = harness.Settings.Get("Test:Hotel:MaxUsers")!;

        setting.Value.Should().Be("50");
        setting.Source.Should().Be(ServerSettingSource.AppSettings);

        var history = await harness.Settings.HistoryAsync(0, Ct);

        history.Items.Select(x => (x.Before, x.After)).Should().Equal(("80", null), (null, "80"));
        history.Items.Should().OnlyContain(x => x.PlayerId == STAFF.Value);
    }

    [Fact]
    public void the_panel_stands_on_its_own_address_and_the_database_so_those_are_startup_settings()
    {
        var harness = Harness(
            "{}",
            (services, configuration) =>
            {
                services.Configure<AdminConfig>(configuration.GetSection(AdminConfig.SECTION_NAME));
                services.Configure<Turbo.Database.Configuration.DatabaseConfig>(
                    configuration.GetSection(
                        Turbo.Database.Configuration.DatabaseConfig.SECTION_NAME
                    )
                );
            }
        );
        var settings = harness.Settings;

        settings.Get("Turbo:Admin:Url")!.Startup.Should().BeTrue();
        settings.Get("Turbo:Admin:PanelUrl")!.Startup.Should().BeTrue();
        settings.Get("Turbo:Admin:SessionHours")!.Startup.Should().BeFalse();
        settings.Get("Turbo:Admin:CatalogLayouts")!.Kind.Should().Be("list");
        settings.Get("Turbo:Database:Migrate")!.Startup.Should().BeTrue();
        settings.Get("Turbo:Database:ConnectionString")!.Secret.Should().BeTrue();
    }

    private SettingsHarness Harness(
        string appSettings = APP_SETTINGS,
        Action<IServiceCollection, IConfiguration>? register = null
    )
    {
        var harness = new SettingsHarness(
            _db,
            appSettings,
            register
                ?? (
                    (services, configuration) =>
                        services.Configure<TestHotelConfig>(
                            configuration.GetSection(TestHotelConfig.SECTION_NAME)
                        )
                )
        );

        _harnesses.Add(harness);

        return harness;
    }

    private SettingsHarness Restart(SettingsHarness harness)
    {
        var restarted = harness.Restart();

        _harnesses.Add(restarted);

        return restarted;
    }

    private void SetVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _variables.Add(name);
    }
}
