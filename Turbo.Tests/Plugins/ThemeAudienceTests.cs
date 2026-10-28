using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Entities.Players;
using Turbo.Gamedata;
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
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Plugins.Messages;
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
    private readonly IThemeClientState _clients = new ThemeClientState();

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
            _clients,
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
            .Contain(["turbo.theme.choose", "turbo.theme.chosen", "turbo.theme.changed"]);
    }

    [Fact]
    public async Task A_theme_s_change_reaches_only_the_players_drawing_it_with_no_reload()
    {
        var live = Live();

        await _themes.SetAsync(
            "night",
            new ThemeAudienceChange(ThemeStatus.Development, Players: [A.Value]),
            STAFF,
            Ct
        );
        // A asked for their theme before connecting; B draws what every client loads.
        await _themes.SessionAsync(A, null, Ct);
        (await live.SettleAsync(Ct)).Players.Should().Be(0, "nothing changed since they loaded");

        // Staff save a patch into night: its file has a new address.
        Repatch("night", "v2");

        var result = await live.SettleAsync(Ct);

        result.Players.Should().Be(1);
        result.Online.Should().Be(2);
        Changed(B).Should().BeEmpty("night is not in B's chain");

        var (theme, revision, url) = Changed(A).Should().ContainSingle().Subject;

        theme.Should().Be("night");
        url.Should().Be($"/gamedata/theme/live/{revision}");
        live.Find(revision)!
            .Variables[ExternalVariablesFile.TEMPLATE_PATCHES]
            .Should()
            .Contain("/night/v2/");

        // A second look with nothing changed sends nothing again.
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        (await live.SettleAsync(Ct)).Players.Should().Be(0);
    }

    [Fact]
    public async Task A_new_default_theme_is_sent_to_the_players_with_no_theme_of_their_own()
    {
        var live = Live();

        await _themes.SetAsync(
            "night",
            new ThemeAudienceChange(ThemeStatus.Development, Players: [A.Value]),
            STAFF,
            Ct
        );
        await _themes.SessionAsync(A, null, Ct);
        await live.SettleAsync(Ct);

        await _themes.SetAsync("blue", new ThemeAudienceChange(IsDefault: true), STAFF, Ct);

        (await live.SettleAsync(Ct)).Players.Should().Be(1);
        Changed(B).Select(x => x.Theme).Should().Equal("blue");
        Changed(A).Should().BeEmpty("A draws night, whose chain the default did not touch");
    }

    [Fact]
    public async Task An_alternative_audience_provider_sends_live_updates_through_the_registered_resolver()
    {
        await _themes.SetAsync(
            "night",
            new ThemeAudienceChange(ThemeStatus.Development),
            STAFF,
            Ct
        );
        var batches = 0;
        _fakes.Handlers["ResolveAsync"] = call =>
        {
            var requests = (IReadOnlyDictionary<PlayerId, string?>)call.Args[0]!;
            requests.Keys.Should().BeEquivalentTo([A, B]);
            batches++;
            return Task.FromResult(new ThemeResolution(null, [(A, "night"), (B, null)]));
        };
        async Task<ThemeSession> SessionOfAsync(PlayerId player)
        {
            var look = await _files.ThemeLookAsync("night", Ct);
            _clients.NoteDrawn(player, null, look.Revision);
            return new ThemeSession("night", look.Variables, [], string.Empty);
        }
        _fakes.Handlers["SessionAsync"] = call => SessionOfAsync((PlayerId)call.Args[0]!);
        _fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)[A, B];
        var replacement = _fakes.Create<IThemeAudiences>();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        new GamedataModule().ConfigureServices(builder.Services, builder);
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IThemeFileService>(_files);
        builder.Services.AddSingleton(_fakes.Create<IGrainFactory>());
        builder.Services.AddSingleton(_fakes.Create<ISessionGateway>());
        builder.Services.AddSingleton(_fakes.Create<IHostApplicationLifetime>());
        builder.Services.AddSingleton<IThemeClientState>(_clients);
        builder.Services.AddSingleton(replacement);
        using var host = builder.Services.BuildServiceProvider();
        host.GetRequiredService<IThemeResolver>().Should().BeSameAs(replacement);
        var live = host.GetRequiredService<IThemeLiveReload>();
        var hosted = (IHostedService)live;
        await host.GetRequiredService<IThemeAudiences>().SessionAsync(A, null, Ct);
        await hosted.StartAsync(Ct);
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        (await live.SettleAsync(Ct)).Players.Should().Be(0);

        Repatch("night", "alternative");
        (await live.SettleAsync(Ct)).Players.Should().Be(1);
        Changed(A).Should().ContainSingle().Which.Theme.Should().Be("night");
        Changed(B).Should().BeEmpty();
        _clients.DrawnBy(A)!.Revision.Should().Be(Changed(A).Single().Revision);

        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        (await live.SettleAsync(Ct)).Players.Should().Be(0);
        Changed(A).Should().ContainSingle("an unchanged revision sends no further message");
        batches.Should().Be(3, "each invalidation resolves all players in one batch");
    }

    [Fact]
    public async Task Live_reload_preserves_accepted_previews_and_rechecks_permission()
    {
        await _themes.SetAsync(
            "night",
            new ThemeAudienceChange(ThemeStatus.Development),
            STAFF,
            Ct
        );
        _previewers.Add(A.Value);
        await _themes.SessionAsync(A, "night", Ct);
        await _themes.SessionAsync(B, "night", Ct);
        _clients.DrawnBy(A)!.Requested.Should().Be("night");
        _clients.DrawnBy(B)!.Requested.Should().BeNull("B's preview was rejected");
        var live = Live();
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        (await live.SettleAsync(Ct)).Players.Should().Be(0);

        Repatch("night", "preview");
        (await live.SettleAsync(Ct)).Players.Should().Be(1);
        Changed(A).Should().ContainSingle().Which.Theme.Should().Be("night");
        Changed(B).Should().BeEmpty();
        _clients.DrawnBy(A)!.Requested.Should().Be("night");

        _previewers.Remove(A.Value);
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        (await live.SettleAsync(Ct)).Players.Should().Be(1);
        Changed(A).Last().Theme.Should().BeEmpty("the preview permission was revoked");
        Changed(B).Should().BeEmpty();

        // A new load replaces the old preview request and shares the revision live reload recorded.
        await _themes.SessionAsync(A, null, Ct);
        _clients.DrawnBy(A)!.Requested.Should().BeNull();
        _clients.DrawnBy(A)!.Revision.Should().Be(Changed(A).Last().Revision);
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        (await live.SettleAsync(Ct)).Players.Should().Be(0);
    }

    [Fact]
    public async Task Offline_players_lose_their_revision_and_preview_before_reconnecting()
    {
        await _themes.SetAsync(
            "night",
            new ThemeAudienceChange(ThemeStatus.Development),
            STAFF,
            Ct
        );
        _previewers.Add(A.Value);
        await _themes.SessionAsync(A, "night", Ct);
        await _themes.SessionAsync(B, null, Ct);
        var live = Live();
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        await live.SettleAsync(Ct);

        _fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)[B];
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
        (await live.SettleAsync(Ct)).Players.Should().Be(0);
        _clients.DrawnBy(A).Should().BeNull();
        _clients.DrawnBy(B).Should().NotBeNull();

        _fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)[A, B];
        (await _themes.SessionAsync(A, null, Ct)).Theme.Should().BeEmpty();
        Repatch("night", "offline");
        (await live.SettleAsync(Ct)).Players.Should().Be(0);
        Changed(A).Should().BeEmpty("the previous load's preview must not survive reconnecting");
    }

    /// <summary>Live reload over this hotel, with A and B online.</summary>
    private ThemeLiveReload Live()
    {
        _fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)[A, B];

        var live = new ThemeLiveReload(
            _files,
            _themes,
            _clients,
            _fakes.Create<IGrainFactory>(),
            Options.Create(new GamedataConfig()),
            _fakes.Create<IHostApplicationLifetime>(),
            TimeProvider.System,
            NullLogger<ThemeLiveReload>.Instance,
            _fakes.Create<ISessionGateway>()
        );

        live.StartAsync(Ct).GetAwaiter().GetResult();
        _handles.Add(live);

        return live;
    }

    /// <summary>The <c>turbo.theme.changed</c> messages a player was sent: theme, revision, address.</summary>
    private List<(string Theme, string Revision, string Url)> Changed(PlayerId player) =>
        [
            .. _fakes
                .SentToPlayer<PluginMessageComposer>(player.Value)
                .Where(x => x.Name == "turbo.theme.changed")
                .Select(x =>
                {
                    var body = x.Body.AsSpan();
                    var fields = new List<string>();

                    while (body.Length >= 2)
                    {
                        var length = System.Buffers.Binary.BinaryPrimitives.ReadInt16BigEndian(
                            body
                        );

                        fields.Add(Encoding.UTF8.GetString(body.Slice(2, length)));
                        body = body[(2 + length)..];
                    }

                    return (fields[0], fields[1], fields[2]);
                }),
        ];

    /// <summary>A plugin's patch file at a new address, as its files loaded again give it.</summary>
    private void Repatch(string key, string version)
    {
        _loaded.SetParts(
            key,
            new Dictionary<string, string>
            {
                [ExternalVariablesFile.TEMPLATE_PATCHES] =
                    $"{{\"{key}\":\"/gamedata/plugins/{key}/{version}/template-patches.json\"}}",
            }
        );
        _files.Invalidate(GamedataFiles.EXTERNAL_VARIABLES);
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
