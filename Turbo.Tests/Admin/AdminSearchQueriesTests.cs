using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Admin.Configuration;
using Turbo.Admin.Players;
using Turbo.Admin.Rooms;
using Turbo.Admin.Search;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Catalog;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's search: one term across players, rooms, catalog pages, furniture, texts and product
/// data, the first few of each, and only the kinds the staff member may open.
/// </summary>
public sealed class AdminSearchQueriesTests : IDisposable
{
    private static readonly SearchScope EVERYTHING = new(true, true, true, true);

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminSearchQueriesTests()
    {
        var db = _catalog.Db;

        db.Insert(Player(1, "Charlie"));
        db.Insert(Player(2, "Chad"));
        db.Insert(Player(3, "alice"));
        db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model_a",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        db.Insert(Room(10, "Chat Lounge", 3));
        db.Insert(Room(11, "Pool", 3));

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)[];
        fakes.Handlers["GetListingViewAsync"] = _ =>
            Task.FromResult(
                new RoomListingViewSnapshot
                {
                    ActiveRooms = [],
                    Epoch = Guid.Empty,
                    Sequence = 0,
                    ChangedKeys = [],
                    IsReset = false,
                }
            );
        fakes.Handlers["SearchAsync"] = call =>
            call.Interface == typeof(IGamedataTextService)
                ? Task.FromResult(
                    new TextSearchResult
                    {
                        Items = [new TextEntrySnapshot { Key = "chat.title", Value = "Chat" }],
                        Total = 1,
                        PageSize = 50,
                    }
                )
                : Task.FromResult(
                    new ProductSearchResult
                    {
                        Items =
                        [
                            new ProductEntrySnapshot
                            {
                                Code = "chair_red",
                                Name = "Red chair",
                                FromHabbo = false,
                            },
                        ],
                        Total = 1,
                        PageSize = 50,
                    }
                );
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task ATerm_FindsEveryKind_InOrder()
    {
        var found = await Search().SearchAsync("cha", EVERYTHING, Ct);

        found
            .Groups.Select(x => x.Kind)
            .Should()
            .Equal("player", "room", "catalogPage", "furniture", "text", "product");
        Titles(found, "player").Should().BeEquivalentTo("Charlie", "Chad");
        Titles(found, "room").Should().Equal("Chat Lounge");
        Titles(found, "catalogPage").Should().Equal("Chairs");
        Titles(found, "furniture").Should().Equal("chair");
        Titles(found, "text").Should().Equal("chat.title");
        found
            .Groups.Single(x => x.Kind == "product")
            .Hits.Single()
            .Should()
            .Be(new SearchHit("chair_red", "chair_red", "Red chair"));
    }

    [Fact]
    public async Task EachKind_ListsTheFirstFew_AndHowManyThereAre()
    {
        var found = await Search(new AdminConfig { SearchHitsPerKind = 1 })
            .SearchAsync("ch", EVERYTHING, Ct);
        var players = found.Groups.Single(x => x.Kind == "player");

        players.Total.Should().Be(2);
        players.Hits.Should().ContainSingle();
    }

    [Fact]
    public async Task OnlyTheKinds_TheStaffMemberMayOpen_AreSearched()
    {
        var found = await Search()
            .SearchAsync(
                "cha",
                new SearchScope(Players: true, Rooms: false, Catalog: false, Gamedata: false),
                Ct
            );

        found.Groups.Select(x => x.Kind).Should().Equal("player");
        _catalog
            .Fakes.Log.Of("SearchAsync")
            .Should()
            .BeEmpty("texts and product data were not theirs to see");
    }

    [Fact]
    public async Task Furniture_IsFound_WithTheCatalogOrGamedata()
    {
        (
            await Search()
                .SearchAsync("chair", new SearchScope(false, false, false, Gamedata: true), Ct)
        )
            .Groups.Select(x => x.Kind)
            .Should()
            .Contain("furniture");
        (
            await Search()
                .SearchAsync("chair", new SearchScope(false, false, Catalog: true, false), Ct)
        )
            .Groups.Select(x => x.Kind)
            .Should()
            .Equal("catalogPage", "furniture");
    }

    [Fact]
    public async Task ACatalogPage_IsFoundByItsName_AndWildcardsAreLiteral()
    {
        // Every fixture page's name is its caption in lower case; "secret" is the hidden page's.
        Titles(await Search().SearchAsync("secret", EVERYTHING, Ct), "catalogPage")
            .Should()
            .Equal("Secret");
        (await Search().SearchAsync("ch_irs", EVERYTHING, Ct))
            .Groups.Should()
            .NotContain(x => x.Kind == "catalogPage");
    }

    [Fact]
    public async Task ATermTooShort_FindsNothing_AndAsksNothing()
    {
        (await Search().SearchAsync(" c ", EVERYTHING, Ct)).Groups.Should().BeEmpty();
        _catalog.Fakes.Log.Of("SearchAsync").Should().BeEmpty();
        _catalog.Fakes.Log.Of("GetListingViewAsync").Should().BeEmpty();
    }

    private static IEnumerable<string> Titles(SearchResponse found, string kind) =>
        found.Groups.Single(x => x.Kind == kind).Hits.Select(x => x.Title);

    private AdminSearchQueries Search(AdminConfig? config = null)
    {
        var options = Options.Create(config ?? new AdminConfig());
        var fakes = _catalog.Fakes;
        var grains = fakes.Create<IGrainFactory>();

        return new AdminSearchQueries(
            _catalog.Db,
            new AdminPlayerQueries(
                _catalog.Db,
                grains,
                fakes.Create<ISessionGateway>(),
                options,
                TimeProvider.System
            ),
            new AdminRoomQueries(_catalog.Db, grains, options, TimeProvider.System),
            new AdminCatalogQueries(
                _catalog.Db,
                _catalog.Definitions,
                fakes.Create<ICatalogEditService>(),
                options
            ),
            fakes.Create<IGamedataTextService>(),
            fakes.Create<IGamedataProductService>(),
            options
        );
    }

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private static RoomEntity Room(int id, string name, int ownerId) =>
        new()
        {
            Id = id,
            Name = name,
            PlayerEntityId = ownerId,
            RoomModelEntityId = 1,
            DoorMode = RoomDoorModeType.Open,
            UsersNow = 0,
            PlayersMax = 25,
            WallHeight = -1,
            HideWalls = false,
            ThicknessWall = RoomThicknessType.Normal,
            ThicknessFloor = RoomThicknessType.Normal,
            AllowBlocking = true,
            AllowPets = true,
            AllowPetsEat = true,
            TradeType = RoomTradeModeType.Disabled,
            MuteType = ModSettingType.Owner,
            KickType = ModSettingType.Owner,
            BanType = ModSettingType.Owner,
            ChatFloodType = ChatFloodSensitivityType.Minimal,
            LastActive = DateTime.UtcNow.AddDays(-id),
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
}
