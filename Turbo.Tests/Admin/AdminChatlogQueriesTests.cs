using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Admin.Rooms;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Staff reading the room chat log: newest first, narrowed to a player (what they said and what
/// was whispered to them), a room or words, a numbered page at a time counted only so far, and
/// one line in its room's context.
/// </summary>
public sealed class AdminChatlogQueriesTests : IDisposable
{
    private const int ALICE = 1;
    private const int BOB = 2;
    private const int CAROL = 3;
    private const int CAFE = 10;
    private const int POOL = 11;

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminChatlogQueriesTests()
    {
        _db.Insert(Player(ALICE, "alice"));
        _db.Insert(Player(BOB, "bob"));
        _db.Insert(Player(CAROL, "carol"));
        _db.Insert(
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
        _db.Insert(Room(CAFE, "Café"));
        _db.Insert(Room(POOL, "Pool"));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TheLog_IsNewestFirst_WithNamesAndWhoAWhisperWasTo()
    {
        _db.Insert(Line(1, ALICE, CAFE, "hello"));
        _db.Insert(Line(2, BOB, CAFE, "psst", target: ALICE));

        var log = await Queries().SearchAsync(null, null, null, 1, null, null, Ct);

        log.Entries.Select(x => x.Message).Should().Equal("psst", "hello");
        log.Entries[0].PlayerName.Should().Be("bob");
        log.Entries[0].RoomName.Should().Be("Café");
        log.Entries[0].TargetPlayerName.Should().Be("alice");
        log.Total.Should().Be(2);
        log.TotalCapped.Should().BeFalse();
        log.Order.Should().Be(new ListOrder("when", ListOrder.DESC));
    }

    [Fact]
    public async Task APlayer_IsWhatTheySaid_AndWhatWasWhisperedToThem()
    {
        _db.Insert(Line(1, ALICE, CAFE, "alice talks"));
        _db.Insert(Line(2, BOB, CAFE, "to alice", target: ALICE));
        _db.Insert(Line(3, BOB, CAFE, "bob talks"));
        _db.Insert(Line(4, CAROL, POOL, "carol talks"));

        (await Queries().SearchAsync("alice", null, null, 1, null, null, Ct))
            .Entries.Select(x => x.Message)
            .Should()
            .Equal("to alice", "alice talks");
        (await Queries().SearchAsync(BOB.ToString(), null, null, 1, null, null, Ct))
            .Entries.Select(x => x.Message)
            .Should()
            .Equal("bob talks", "to alice");
        (await Queries().SearchAsync("nobody", null, null, 1, null, null, Ct))
            .Entries.Should()
            .BeEmpty();
    }

    [Fact]
    public async Task ARoomAndWords_NarrowIt_WithWildcardsMatchedLiterally()
    {
        _db.Insert(Line(1, ALICE, CAFE, "100% sure"));
        _db.Insert(Line(2, ALICE, CAFE, "1000 sure"));
        _db.Insert(Line(3, ALICE, POOL, "100% wet"));

        (await Queries().SearchAsync(null, CAFE, "100%", 1, null, null, Ct))
            .Entries.Select(x => x.Message)
            .Should()
            .Equal("100% sure");
        (await Queries().SearchAsync(null, null, "SURE", 1, null, null, Ct))
            .Entries.Should()
            .HaveCount(2, "case is ignored");
    }

    [Fact]
    public async Task Pages_AreNumbered_AndTheLogReadsOldestFirstWhenAsked()
    {
        for (var id = 1; id <= 5; id++)
            _db.Insert(Line(id, ALICE, CAFE, $"line {id}"));

        var queries = Queries(pageSize: 2);
        var second = await queries.SearchAsync(null, null, null, 2, null, null, Ct);

        second.Entries.Select(x => x.Id).Should().Equal(3, 2);
        (second.Total, second.Page, second.PageSize).Should().Be((5, 2, 2));

        var oldest = await queries.SearchAsync(null, null, null, 1, "when", ListOrder.ASC, Ct);

        oldest.Entries.Select(x => x.Id).Should().Equal(1, 2);
        oldest.Order.Should().Be(new ListOrder("when", ListOrder.ASC));

        // A page past the last is the last; a column the log does not sort by is its default.
        (await queries.SearchAsync(null, null, null, 9, null, null, Ct))
            .Entries.Select(x => x.Id)
            .Should()
            .Equal(1);
        (await queries.SearchAsync(null, null, null, 1, "message", ListOrder.ASC, Ct))
            .Order.Should()
            .Be(new ListOrder("when", ListOrder.DESC));
    }

    [Fact]
    public async Task ALogPastTheCountLimit_IsCountedToIt_AndPagesNoFurther()
    {
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            db.Chatlogs.AddRange(
                Enumerable
                    .Range(1, LogCounts.LIMIT + 5)
                    .Select(id => Line(id, ALICE, CAFE, $"line {id}"))
            );
            await db.SaveChangesAsync(Ct);
        }

        var queries = Queries(pageSize: 100);
        var first = await queries.SearchAsync(null, null, null, 1, null, null, Ct);

        first.Total.Should().Be(LogCounts.LIMIT);
        first.TotalCapped.Should().BeTrue();

        var last = await queries.SearchAsync(null, null, null, 1_000, null, null, Ct);

        last.Page.Should().Be(LogCounts.LIMIT / 100, "the pages stop where the count does");
        last.Entries.Select(x => x.Id).Should().HaveCount(100).And.StartWith(105);

        // Turned round, the oldest are a page away.
        (await queries.SearchAsync(null, null, null, 1, "when", ListOrder.ASC, Ct))
            .Entries[0]
            .Id.Should()
            .Be(1);
    }

    [Fact]
    public async Task ALineInContext_IsTheLinesAroundItInItsRoomOnly()
    {
        for (var id = 1; id <= 9; id++)
            _db.Insert(Line(id, ALICE, id % 2 == 0 ? POOL : CAFE, $"line {id}"));

        // Café has 1, 3, 5, 7, 9; one each side of 5 with a page of 2.
        var around = await Queries(pageSize: 2).AroundAsync(5, Ct);

        around.Entries.Select(x => x.Id).Should().Equal(7, 5, 3);
        around.HasOlder.Should().BeTrue();
        around.HasNewer.Should().BeTrue();

        (await Queries().AroundAsync(999, Ct)).Entries.Should().BeEmpty();
    }

    private AdminChatlogQueries Queries(int pageSize = 100) =>
        new(_db, Options.Create(new AdminConfig { ChatlogPageSize = pageSize }));

    private static RoomChatlogEntity Line(
        int id,
        int playerId,
        int roomId,
        string message,
        int? target = null
    ) =>
        new()
        {
            Id = id,
            PlayerEntityId = playerId,
            RoomEntityId = roomId,
            TargetPlayerEntityId = target,
            Message = message,
            PlayerEntity = null!,
            RoomEntity = null!,
        };

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private static RoomEntity Room(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            PlayerEntityId = ALICE,
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
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
}
