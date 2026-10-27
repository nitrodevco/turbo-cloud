using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Entities.Players;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Files;
using Turbo.Gamedata.Furniture;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Variables;
using Turbo.Messages.PluginMessages;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// Who gets which theme (<c>docs/plugin-data.md</c>): a development theme's look reaches only its
/// audience, a player draws a theme they chose while it may be chosen, the default theme reaches
/// everyone, and every client but those loads the default's variables. XenForo's styles are the
/// model (a style's chain, one default, user selectable); rules of our framework, not Habbo's.
/// </summary>
[Trait("evidence", "inferred")]
public sealed class ThemeAudienceTests : IDisposable
{
    private static readonly PlayerId STAFF = PlayerId.Parse(1);
    private static readonly PlayerId A = PlayerId.Parse(101);
    private static readonly PlayerId B = PlayerId.Parse(102);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly LoadedPluginGamedata _loaded = new();
    private readonly List<IDisposable> _handles = [];
    private readonly HashSet<long> _previewers = [];
    private readonly GamedataFileService _files;
    private readonly ThemeAudienceService _themes;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ThemeAudienceTests()
    {
        foreach (var id in new[] { STAFF, A, B })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id.Value,
                    Name = $"player{id.Value}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(
                (string)call.Args[0]! == PermissionNodes.Appearance.THEMES_PREVIEW
                    && _previewers.Contains(Convert.ToInt64(call.Key))
            );

        _files = new GamedataFileService(
            _db,
            Options.Create(new GamedataConfig()),
            new FurnitureOfferCatalog(
                _fakes.Create<ICatalogSnapshotProvider<NormalCatalog>>(),
                _fakes.Create<ICatalogSnapshotProvider<BuildersClubCatalog>>()
            ),
            _fakes.Create<IServerSettings>(),
            _fakes.Create<IAssetBundleStore>(),
            TimeProvider.System,
            _loaded,
            NullLogger<GamedataFileService>.Instance
        );
        _themes = new ThemeAudienceService(
            _db,
            _files,
            _loaded,
            _fakes.Create<IGrainFactory>(),
            TimeProvider.System,
            NullLogger<ThemeAudienceService>.Instance
        );

        // A hotel that had two themes (each extends Habbo's own) and a plugin that is no theme (every
        // client gets its patches) when the migration ran: the first start keeps both themes Live.
        _db.Insert(
            new GamedataThemeEntity { PluginKey = ThemeRows.ADOPT, UpdatedAt = DateTime.UtcNow }
        );
        Load("night", theme: true);
        Load("blue", theme: true);
        Load("chat", theme: false);
        _themes.AdoptAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        foreach (var handle in _handles)
            handle.Dispose();

        _db.Dispose();
    }

    [Fact]
    public async Task A_development_theme_reaches_only_its_audience()
    {
        await _themes.SetAsync(
            "night",
            new ThemeAudienceChange(ThemeStatus.Development, Players: [A.Value]),
            STAFF,
            Ct
        );

        // Everyone's file: the other themes and the plugin, not the one in development.
        (await PatchesInFileAsync())
            .Should()
            .BeEquivalentTo("blue", "chat");

        var a = await _themes.SessionAsync(A, null, Ct);

        a.Theme.Should().Be("night");
        Owners(a.Variables![ExternalVariablesFile.TEMPLATE_PATCHES])
            .Should()
            .BeEquivalentTo("night", "chat");
        a.Choices.Select(x => x.Key).Should().Equal("night");

        var b = await _themes.SessionAsync(B, null, Ct);

        b.Theme.Should().BeEmpty();
        b.Variables.Should().BeNull("B draws what every client loads");
        b.Choices.Should().BeEmpty();

        // Staff who may preview themes get it for a load they ask it for, not otherwise.
        _previewers.Add(STAFF.Value);
        (await _themes.SessionAsync(STAFF, null, Ct)).Theme.Should().BeEmpty();
        (await _themes.SessionAsync(STAFF, "night", Ct)).Theme.Should().Be("night");
        (await _themes.SessionAsync(B, "night", Ct)).Theme.Should().BeEmpty();
    }

    [Fact]
    public async Task A_permission_group_in_the_audience_reaches_players_who_inherit_it()
    {
        // builders (2) inherits nothing; A holds senior (3), whose parent is builders; B holds none.
        PermissionGroupSnapshot Group(int id, string name, params int[] parents) =>
            new()
            {
                Id = id,
                Name = name,
                DisplayName = name,
                Weight = id,
                ParentIds = [.. parents],
                Nodes = [],
                Meta = [],
            };

        _fakes.Handlers["GetSnapshotAsync"] = _ =>
            Task.FromResult(
                PermissionGroupDirectorySnapshot.EMPTY with
                {
                    Groups = ImmutableDictionary<int, PermissionGroupSnapshot>
                        .Empty.Add(2, Group(2, "builders"))
                        .Add(3, Group(3, "senior", 2)),
                }
            );
        _fakes.Handlers["GetAssignmentsAsync"] = call =>
            Task.FromResult(
                PlayerPermissionAssignmentsSnapshot.EMPTY with
                {
                    Groups =
                        Convert.ToInt64(call.Key) == A.Value
                            ? [new PermissionGroupMembershipSnapshot { GroupId = 3 }]
                            : [],
                }
            );

        await _themes.SetAsync(
            "night",
            new ThemeAudienceChange(ThemeStatus.Development, Groups: ["builders"]),
            STAFF,
            Ct
        );

        (await _themes.SessionAsync(A, null, Ct)).Theme.Should().Be("night");
        (await _themes.SessionAsync(B, null, Ct)).Theme.Should().BeEmpty();
    }

    [Fact]
    public async Task A_player_s_choice_of_a_theme_that_may_be_chosen_is_kept_and_drawn()
    {
        await _themes.SetAsync("blue", new ThemeAudienceChange(Selectable: true), STAFF, Ct);

        var refused = () => _themes.ChooseAsync(B, "night", Ct);

        await refused.Should().ThrowAsync<ArgumentException>("night may not be chosen");

        await _themes.ChooseAsync(B, "blue", Ct);

        var b = await _themes.SessionAsync(B, null, Ct);

        b.Theme.Should().Be("blue");
        b.Chosen.Should().Be("blue");
        Owners(b.Variables![ExternalVariablesFile.TEMPLATE_PATCHES])
            .Should()
            .BeEquivalentTo("blue", "chat");

        // A theme that may no longer be chosen leaves them with the hotel's look.
        await _themes.SetAsync("blue", new ThemeAudienceChange(Selectable: false), STAFF, Ct);

        (await _themes.SessionAsync(B, null, Ct)).Theme.Should().BeEmpty();
    }

    [Fact]
    public async Task The_default_theme_reaches_everyone_and_there_is_one()
    {
        // No default: every Live theme, as before themes had an audience.
        (await PatchesInFileAsync())
            .Should()
            .BeEquivalentTo("night", "blue", "chat");

        var notLive = () =>
            _themes.SetAsync(
                "night",
                new ThemeAudienceChange(ThemeStatus.Development, IsDefault: true),
                STAFF,
                Ct
            );

        await notLive.Should().ThrowAsync<ArgumentException>();

        await _themes.SetAsync("night", new ThemeAudienceChange(IsDefault: true), STAFF, Ct);
        (await PatchesInFileAsync()).Should().BeEquivalentTo("night", "chat");
        (await _themes.SessionAsync(A, null, Ct)).Variables.Should().BeNull();

        await _themes.SetAsync("blue", new ThemeAudienceChange(IsDefault: true), STAFF, Ct);
        (await PatchesInFileAsync()).Should().BeEquivalentTo("blue", "chat");
        (await _themes.ListAsync(Ct))
            .Where(x => x.IsDefault)
            .Select(x => x.Key)
            .Should()
            .Equal("blue");
    }

    [Fact]
    public async Task A_theme_added_after_the_first_start_reaches_no_player_until_staff_set_it_live()
    {
        // Made in the panel or installed now: no row, so in development with no audience.
        Load("fresh", theme: true);

        (await _themes.ListAsync(Ct))
            .Single(x => x.Key == "fresh")
            .Status.Should()
            .Be(ThemeStatus.Development);
        (await PatchesInFileAsync()).Should().BeEquivalentTo("night", "blue", "chat");

        var b = await _themes.SessionAsync(B, null, Ct);

        b.Theme.Should().BeEmpty();
        b.Choices.Should().BeEmpty();

        // Staff who may preview themes may choose it; once Live it is every player's again.
        _previewers.Add(STAFF.Value);
        (await _themes.SessionAsync(STAFF, null, Ct))
            .Choices.Select(x => x.Key)
            .Should()
            .Equal("fresh");

        await _themes.SetAsync("fresh", new ThemeAudienceChange(ThemeStatus.Live), STAFF, Ct);

        (await PatchesInFileAsync()).Should().BeEquivalentTo("night", "blue", "fresh", "chat");

        // The hotel's own theme is in every chain it ends in: never in development or off.
        var off = () =>
            _themes.SetAsync(
                ThemeChains.HOTEL,
                new ThemeAudienceChange(ThemeStatus.Off),
                STAFF,
                Ct
            );

        await off.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Turbo_s_own_modules_declare_named_messages_under_turbo()
    {
        var extensions = new ExtensionPacketRegistry();
        var directory = new PluginMessageDirectory(extensions);
        var host = new ServiceCollection()
            .AddSingleton(_fakes.Create<IGrainFactory>())
            .AddSingleton<IThemeAudiences>(_themes)
            .BuildServiceProvider();
        var processor = new PluginMessageFeatureProcessor(
            new MessageRegistry(host, NullLogger<MessageRegistry>.Instance),
            directory,
            NullLoggerFactory.Instance,
            host
        );

        using var registered = await processor.ProcessAsync(typeof(ChooseTheme).Assembly, host, Ct);

        directory
            .Messages.Select(x => x.Name)
            .Should()
            .Contain(["turbo.theme.choose", "turbo.theme.chosen"]);
    }

    private void Load(string key, bool theme)
    {
        _handles.Add(_loaded.Add(key, () => _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES)));
        _loaded.SetTheme(key, theme, theme ? ThemeStyles.BASE : null);
        _loaded.SetParts(
            key,
            new Dictionary<string, string>
            {
                [ExternalVariablesFile.TEMPLATE_PATCHES] =
                    $"{{\"{key}\":\"/gamedata/plugins/{key}/template-patches.json\"}}",
            }
        );
    }

    private async Task<IEnumerable<string>> PatchesInFileAsync()
    {
        var file = await _files.GetCurrentAsync(GamedataFiles.EXTERNAL_VARIABLES, Ct);

        using var json = JsonDocument.Parse(
            Encoding.UTF8.GetString(Turbo.Gamedata.GamedataBytes.Decompress(file.Gzipped))
        );

        return Owners(
            json.RootElement.GetProperty(ExternalVariablesFile.TEMPLATE_PATCHES).GetRawText()
        );
    }

    private static IEnumerable<string> Owners(string patches)
    {
        using var json = JsonDocument.Parse(patches);

        return [.. json.RootElement.EnumerateObject().Select(x => x.Name)];
    }
}
