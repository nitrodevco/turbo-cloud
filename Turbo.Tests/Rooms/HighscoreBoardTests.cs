using FluentAssertions;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Highscore;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Wired highscore boards. The Wired Faculty tutorial "Add a specific score to a leaderboard"
/// (12/03/2025) writes a player's score to a "Highscore Per-team" board: "The leaderboard gets
/// updated every time a counter ends in the room". No board had any logic, so nothing was ever
/// written to one.
/// </summary>
public sealed class HighscoreBoardTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("highscore_perteam*1", HighscoreBoards.PER_TEAM, HighscoreBoards.CLEAR_NEVER)]
    [InlineData("highscore_mostwin*2", HighscoreBoards.MOST_WINS, HighscoreBoards.CLEAR_DAILY)]
    [InlineData("highscore_classic*3", HighscoreBoards.CLASSIC, HighscoreBoards.CLEAR_WEEKLY)]
    [InlineData(
        "highscore_fastesttime*4",
        HighscoreBoards.FASTEST_TIME,
        HighscoreBoards.CLEAR_MONTHLY
    )]
    [InlineData(
        "highscore_longesttime*1",
        HighscoreBoards.LONGEST_TIME,
        HighscoreBoards.CLEAR_NEVER
    )]
    public void A_boards_classname_names_its_kind_and_period(
        string classname,
        int scoreType,
        int clearType
    )
    {
        HighscoreBoards
            .TryParse(classname, out var parsedScore, out var parsedClear)
            .Should()
            .BeTrue();
        (parsedScore, parsedClear).Should().Be((scoreType, clearType));
    }

    [Fact]
    public void Each_kind_writes_a_game_its_own_way()
    {
        IReadOnlyList<(int, IReadOnlyList<string>)> game1 =
        [
            (30, ["bob", "alice"]),
            (10, ["carol"]),
        ];
        IReadOnlyList<(int, IReadOnlyList<string>)> game2 =
        [
            (20, ["alice", "bob"]),
            (40, ["carol"]),
        ];

        Rows(HighscoreBoards.PER_TEAM, game1, 100, game2, 50)
            .Should()
            .Equal("40 carol", "30 alice,bob");
        Rows(HighscoreBoards.MOST_WINS, game1, 100, game2, 50)
            .Should()
            .Equal("1 alice,bob", "1 carol");
        Rows(HighscoreBoards.CLASSIC, game1, 100, game2, 50)
            .Should()
            .Equal("40 carol", "30 alice,bob", "20 alice,bob", "10 carol");
        // The time boards time the winners, fastest first.
        Rows(HighscoreBoards.FASTEST_TIME, game1, 100, game2, 50)
            .Should()
            .Equal("50 carol", "100 alice,bob");
        Rows(HighscoreBoards.LONGEST_TIME, game1, 100, game1, 70).Should().Equal("100 alice,bob");
    }

    [Fact]
    public async Task Ending_a_game_writes_the_teams_to_the_boards_in_the_room()
    {
        var room = new WiredRoom(8, 8);
        var alice = Player(room, 5, "alice");
        var bob = Player(room, 6, "bob");
        var board = (FurnitureHighscoreLogic)
            room.AddFloorItem(
                40,
                7,
                7,
                "highscore_perteam*1",
                createLogic: (factory, ctx) => new FurnitureHighscoreLogic(factory, ctx)
            ).Logic;
        var game = room.Harness.Module<RoomGameSystem>();

        await game.JoinTeamAsync(alice.PlayerId, GameTeamType.Red, Ct);
        await game.JoinTeamAsync(bob.PlayerId, GameTeamType.Blue, Ct);
        await game.StartGameAsync(Ct);
        await game.SetScoreAsync(GameTeamType.Red, 25, Ct);
        await game.SetScoreAsync(GameTeamType.Blue, 40, Ct);
        await game.EndGameAsync(Ct);

        board.Entries.Select(Row).Should().Equal("40 bob", "25 alice");

        var snapshot = (HighscoreStuffSnapshot)board.StuffData.GetSnapshot();

        (snapshot.ScoreType, snapshot.ClearType)
            .Should()
            .Be((HighscoreBoards.PER_TEAM, HighscoreBoards.CLEAR_NEVER));
        snapshot.Entries.Select(x => x.Score).Should().Equal(40, 25);
    }

    [Fact]
    public void A_board_gets_the_highscore_logic_by_its_classname()
    {
        var room = new WiredRoom();
        var item = room.AddFloorItem(40, 1, 1, "highscore_classic*2");

        room.Harness.LogicProvider.CreateLogicInstance(
                "default_floor",
                new Turbo.Rooms.Object.Furniture.Floor.RoomFloorItemContext(room.Harness.Room, item)
            )
            .Should()
            .BeOfType<FurnitureHighscoreLogic>();
    }

    private static RoomPlayerAvatar Player(WiredRoom room, int index, string name)
    {
        var avatar = room.Enter(index, index - 4, 1);

        avatar.GetType().GetProperty("Name")!.SetValue(avatar, name);

        return avatar;
    }

    private static List<string> Rows(
        int scoreType,
        IReadOnlyList<(int, IReadOnlyList<string>)> first,
        int firstSeconds,
        IReadOnlyList<(int, IReadOnlyList<string>)> second,
        int secondSeconds
    )
    {
        var rows = HighscoreBoards.Record(scoreType, [], first, firstSeconds);

        return [.. HighscoreBoards.Record(scoreType, rows, second, secondSeconds).Select(Row)];
    }

    private static string Row(HighscoreEntry entry) =>
        $"{entry.Score} {string.Join(',', entry.Users)}";
}
