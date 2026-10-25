using FluentAssertions;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Games;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room games' furni on the room's one set of teams: the team gates (<c>bb_gate_*</c>,
/// <c>es_gate_*</c>), the team scoreboards (<c>bb_score_*</c>) and the Battle Banzai tiles
/// (<c>bb_patch1</c>), reached as a player reaches them, by walking.
/// </summary>
public sealed class BattleBanzaiGameTests
{
    private const int PLAYER = 1;
    private const int SECOND = 2;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(10, 10, avatarContexts: true);
    private long _now;

    private RoomGameSystem Game => _room.Harness.Module<RoomGameSystem>();

    // The Banzai green gate wears Sulake's furni data's "2,34" (bb_gate_g: team 2, effect 34);
    // a player on the Freeze red gate wore 40 (official client capture 2026-10-18T09-00-17).
    [Theory]
    [InlineData("bb_gate_g", GameTeamType.Green, 34)]
    [InlineData("es_gate_r", GameTeamType.Red, 40)]
    public async Task Stepping_on_a_gate_puts_the_player_in_its_team_wearing_that_games_effect(
        string gate,
        GameTeamType team,
        int effect
    )
    {
        var avatar = _room.Enter(PLAYER, 1, 1);
        AddGate(10, 3, 1, gate);

        await WalkToAsync(avatar, 3, 1);

        Game.GetTeam((RoomObjectId)PLAYER).Should().Be(team);
        avatar.EffectId.Should().Be(effect);
    }

    // The Banzai and the Freeze gate of a colour showed the same count in four rooms, and a player
    // joining green moved both green gates from 3 to 4 in one ObjectsDataUpdate (official client
    // captures 2026-10-17T18-24-44, 22-42-57, 22-45-40, 22-46-19).
    [Fact]
    public async Task Both_gates_of_a_colour_count_the_team_and_change_together_in_one_update()
    {
        var avatar = _room.Enter(PLAYER, 1, 1);
        AddGate(10, 3, 1, "bb_gate_g");
        AddGate(11, 6, 6, "es_gate_g");
        AddGate(12, 7, 7, "bb_gate_r");

        await WalkToAsync(avatar, 3, 1);

        StateOf(10).Should().Be("1");
        StateOf(11).Should().Be("1");
        StateOf(12).Should().Be("0");
        GateUpdates()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo([(10, "1"), (11, "1")]);
    }

    [Fact]
    public async Task A_gate_counts_a_player_out_when_they_leave_the_room()
    {
        var avatar = _room.Enter(PLAYER, 1, 1);
        AddGate(10, 3, 1, "bb_gate_y");
        await WalkToAsync(avatar, 3, 1);

        await Game.OnRoomEventAsync(
            new PlayerLeftEvent
            {
                RoomId = _room.Harness.Room.RoomId,
                CausedBy = _room.ContextFor(PLAYER),
                ObjectId = (RoomObjectId)PLAYER,
                PlayerId = avatar.PlayerId,
            },
            Ct
        );

        StateOf(10).Should().Be("0");
    }

    // "Displays the pink team's score" (furnidata bb_score_r); scoreboards were updated by
    // ObjectsDataUpdate (official client capture 2026-10-17T22-42-42, fball_score_y).
    [Fact]
    public async Task A_scoreboard_shows_its_teams_score_and_a_new_game_puts_it_back_at_0()
    {
        _room.AddFloorItem(
            20,
            8,
            8,
            FurnitureTeamScoreLogic.LOGIC_NAME,
            className: "bb_score_r",
            totalStates: 1,
            createLogic: (s, c) => new FurnitureTeamScoreLogic(s, c)
        );

        await Game.StartGameAsync(Ct);
        await Game.GiveScoreAsync(GameTeamType.Red, 3, default, default, 0, Ct);
        await Game.GiveScoreAsync(GameTeamType.Green, 5, default, default, 0, Ct);

        StateOf(20).Should().Be("3");

        await Game.StartGameAsync(Ct);

        StateOf(20).Should().Be("0");
        Updates().Select(x => x.Single()).Should().Equal((20, "3"), (20, "0"));
    }

    [Fact]
    public void Every_game_furni_class_has_the_logic_of_its_class()
    {
        string[] offenders =
        [
            .. new (string Name, string Logic)[]
            {
                ("bb_patch1", FurnitureBattleBanzaiTileLogic.LOGIC_NAME),
                ("bb_gate_r", FurnitureTeamGateLogic.LOGIC_NAME),
                ("bb_gate_y", FurnitureTeamGateLogic.LOGIC_NAME),
                ("es_gate_b", FurnitureTeamGateLogic.LOGIC_NAME),
                ("bb_score_g", FurnitureTeamScoreLogic.LOGIC_NAME),
                ("es_score_y", FurnitureTeamScoreLogic.LOGIC_NAME),
                ("bb_counter", "none"),
                ("bb_gate_x", "none"),
            }
                .Where(x => (GameFurniture.LogicOf(x.Name) ?? "none") != x.Logic)
                .Select(x => x.Name),
        ];

        offenders.Should().BeEmpty();
        FurnitureGameTimerLogic.CLASSNAMES.Should().Contain("bb_counter");
    }

    [Fact]
    [Trait("evidence", "inferred")]
    public async Task Three_steps_of_one_team_lock_a_tile_for_a_point_and_another_team_cannot_take_it()
    {
        var avatar = _room.Enter(PLAYER, 1, 1);
        var other = _room.Enter(SECOND, 1, 5);
        AddTile(30, 2, 1);
        await Game.JoinTeamAsync(
            (RoomObjectId)PLAYER,
            GameTeamType.Blue,
            WiredTeamType.BattleBanzai,
            Ct
        );
        await Game.JoinTeamAsync(
            (RoomObjectId)SECOND,
            GameTeamType.Red,
            WiredTeamType.BattleBanzai,
            Ct
        );
        await Game.StartGameAsync(Ct);

        StateOf(30).Should().Be("1");

        List<string> states = [];

        for (var step = 0; step < 3; step++)
        {
            await WalkToAsync(avatar, 2, 1);
            states.Add(StateOf(30));
            await WalkToAsync(avatar, 1, 1);
        }

        await WalkToAsync(other, 2, 1);

        states.Should().Equal("9", "10", "11");
        StateOf(30).Should().Be("11");
        Game.GetScore(GameTeamType.Blue).Should().Be(1);
        Game.GetScore(GameTeamType.Red).Should().Be(0);
        TilesLockedFacts().Should().Equal((100 + PLAYER, "battleballtileslocked", 1L));
    }

    [Fact]
    [Trait("evidence", "inferred")]
    public async Task Tiles_closed_in_by_a_teams_locked_tiles_lock_for_it()
    {
        var avatar = _room.Enter(PLAYER, 4, 0);

        // A 3 x 3 field from (1,1); every tile but the middle one already locked green, bar (3,2).
        var id = 40;
        for (var x = 1; x <= 3; x++)
        for (var y = 1; y <= 3; y++)
            AddTile(id++, x, y);

        await Game.JoinTeamAsync(
            (RoomObjectId)PLAYER,
            GameTeamType.Green,
            WiredTeamType.BattleBanzai,
            Ct
        );
        await Game.StartGameAsync(Ct);

        foreach (var tile in Tiles().Where(t => t.Item is not ((2, 2) or (3, 2))))
            await tile.Logic.SetStateAsync(
                FurnitureBattleBanzaiTileLogic.StateOf(GameTeamType.Green, 2)
            );

        var gap = Tiles().Single(t => t.Item == (3, 2)).Logic;
        await gap.SetStateAsync(FurnitureBattleBanzaiTileLogic.StateOf(GameTeamType.Green, 1));

        await WalkToAsync(avatar, 4, 2);
        await WalkToAsync(avatar, 3, 2);

        Tiles().Single(t => t.Item == (2, 2)).Logic.IsLocked.Should().BeTrue();
        Tiles().Single(t => t.Item == (2, 2)).Logic.Colour.Should().Be(GameTeamType.Green);
        Game.GetScore(GameTeamType.Green).Should().Be(2);
        TilesLockedFacts().Should().Equal((100 + PLAYER, "battleballtileslocked", 2L));
    }

    private IEnumerable<((int X, int Y) Item, FurnitureBattleBanzaiTileLogic Logic)> Tiles() =>
        _room
            .Harness.State.ItemsById.Values.OfType<IRoomFloorItem>()
            .Where(x => x.Logic is FurnitureBattleBanzaiTileLogic)
            .Select(x => ((x.X, x.Y), (FurnitureBattleBanzaiTileLogic)x.Logic))
            .ToList();

    private List<(int Player, string Value, long Amount)> TilesLockedFacts() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("Record")
                .Select(x =>
                    (Player: ((PlayerId)x.Args[1]!).Value, Fact: (AchievementFact)x.Args[2]!)
                )
                .Where(x => x.Fact.Source == AchievementSources.BATTLEBALL_TILE_LOCKED)
                .Select(x => (x.Player, x.Fact.Value, x.Fact.Amount)),
        ];

    private void AddGate(int id, int x, int y, string className) =>
        _room.AddFloorItem(
            id,
            x,
            y,
            FurnitureTeamGateLogic.LOGIC_NAME,
            className: className,
            totalStates: 6,
            createLogic: (s, c) => new FurnitureTeamGateLogic(s, c)
        );

    private void AddTile(int id, int x, int y) =>
        _room.AddFloorItem(
            id,
            x,
            y,
            FurnitureBattleBanzaiTileLogic.LOGIC_NAME,
            definitionId: 3633,
            className: FurnitureBattleBanzaiTileLogic.CLASSNAME,
            totalStates: 16,
            createLogic: (s, c) => new FurnitureBattleBanzaiTileLogic(s, c)
        );

    private string StateOf(int id) => _room.FloorItem(id).Logic.StuffData.GetLegacyString();

    private List<List<(int, string)>> Updates() =>
        [
            .. _room
                .Harness.Fakes.SentToRoom<ObjectsDataUpdateMessageComposer>()
                .Select(x =>
                    x.StuffDatas.Select(s => ((int)s.Item1, ((LegacyStuffSnapshot)s.Item2).Data))
                        .ToList()
                ),
        ];

    private List<List<(int, string)>> GateUpdates() =>
        [.. Updates().Where(x => x.Any(s => s.Item1 is >= 10 and < 20))];

    private async Task WalkToAsync(RoomPlayerAvatar avatar, int x, int y)
    {
        (await _room.Harness.Room.WalkAvatarToAsync(_room.ContextFor(avatar), x, y, Ct))
            .Should()
            .BeTrue();

        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }
    }
}
