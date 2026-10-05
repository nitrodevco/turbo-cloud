using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Admin.Commands;
using Turbo.Admin.Configuration;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Staff reading the command log: newest first, with the names of who ran each command and where,
/// narrowed to one player, command, outcome or source, a page at a time.
/// </summary>
public sealed class AdminCommandLogQueriesTests : IDisposable
{
    private const int ALICE = 1;
    private const int MOD = 4;
    private const int CAFE = 10;

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminCommandLogQueriesTests()
    {
        _db.Insert(Player(ALICE, "alice"));
        _db.Insert(Player(MOD, "moderator"));
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
        _db.Insert(Room(CAFE, "Café", ALICE));

        _db.Insert(Log(1, MOD, CAFE, "ban", "alice 1d", "completed", "player"));
        _db.Insert(Log(2, 0, 0, "shutdown", "5", "completed", "console"));
        _db.Insert(Log(3, MOD, 0, "alert", "alice hi", "completed", "panel"));
        _db.Insert(Log(4, MOD, CAFE, "kick", "alice", "refused", null));
        // Run by a player, in a room, that are both gone since.
        _db.Insert(Log(5, 99, 77, "warn", "x", "failed", "player"));
    }

    public void Dispose() => _db.Dispose();

    private AdminCommandLogQueries Queries(int pageSize = 50) =>
        new(_db, Options.Create(new AdminConfig { CommandLogPageSize = pageSize }));

    private async Task<int[]> IdsAsync(
        string? player = null,
        string? command = null,
        string? outcome = null,
        string? source = null
    ) =>
        [
            .. (
                await Queries().SearchAsync(player, command, outcome, source, 1, Ct)
            ).Entries.Select(x => x.Id),
        ];

    [Fact]
    public async Task TheLog_IsNewestFirst_WithWhoRanEachAndWhere()
    {
        var log = await Queries().SearchAsync(null, null, null, null, 1, Ct);

        log.Total.Should().Be(5);
        log.Entries.Select(x => x.Id).Should().Equal(5, 4, 3, 2, 1);

        var ban = log.Entries.Single(x => x.Id == 1);
        ban.PlayerName.Should().Be("moderator");
        ban.RoomName.Should().Be("Café");
        ban.Command.Should().Be("ban");
        ban.Arguments.Should().Be("alice 1d");

        var console = log.Entries.Single(x => x.Id == 2);
        console.PlayerId.Should().Be(0);
        console.PlayerName.Should().BeNull();
        console.RoomName.Should().BeNull();

        var gone = log.Entries.Single(x => x.Id == 5);
        gone.PlayerName.Should().BeNull("the player is gone, but the entry stays");
        gone.RoomName.Should().BeNull();
    }

    [Fact]
    public async Task APlayer_IsFoundByNameOrId_AndAnUnknownNameFindsNothing()
    {
        (await IdsAsync(player: "moderator")).Should().Equal(4, 3, 1);
        (await IdsAsync(player: " 4 ")).Should().Equal(4, 3, 1);
        (await IdsAsync(player: "nobody")).Should().BeEmpty();
    }

    [Fact]
    public async Task ACommand_IsMatchedAsTypedInGame_OrAsListed()
    {
        (await IdsAsync(command: ":ban")).Should().Equal(1);
        (await IdsAsync(command: "Ban")).Should().Equal(1);
    }

    [Fact]
    public async Task AnOutcome_OrASource_NarrowsIt_AndChatMeansTypedInARoom()
    {
        (await IdsAsync(outcome: "refused")).Should().Equal(4);
        (await IdsAsync(source: "panel")).Should().Equal(3);
        (await IdsAsync(source: "chat")).Should().Equal(4);
        (await IdsAsync(player: "moderator", source: "player")).Should().Equal(1);
    }

    [Fact]
    public async Task ThePages_FollowOnFromEachOther()
    {
        var second = await Queries(pageSize: 2).SearchAsync(null, null, null, null, 2, Ct);

        second.Total.Should().Be(5);
        second.PageSize.Should().Be(2);
        second.Entries.Select(x => x.Id).Should().Equal(3, 2);
    }

    private static CommandLogEntity Log(
        int id,
        int playerId,
        int roomId,
        string command,
        string arguments,
        string outcome,
        string? source
    ) =>
        new()
        {
            Id = id,
            PlayerEntityId = playerId,
            RoomEntityId = roomId,
            Command = command,
            Arguments = arguments,
            Outcome = outcome,
            Source = source,
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
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
}
