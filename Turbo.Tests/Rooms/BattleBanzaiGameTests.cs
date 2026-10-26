using FluentAssertions;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
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

    // results! (red) on 6,4 four times: 3, 4, 5 = locked with SCORE_R=1 in the same
    // ObjectsDataUpdate, then nothing; a locked tile of another team never changed (official
    // client capture 2026-10-26, session 012541 t 947.5-975.5, 2/2 teams; t 1069.0-1104.0, 4/4).
    [Fact]
    public async Task Three_steps_of_one_team_lock_a_tile_for_a_point_and_another_team_cannot_take_it()
    {
        var avatar = _room.Enter(PLAYER, 1, 1);
        var other = _room.Enter(SECOND, 1, 5);
        AddTile(30, 2, 1);
        AddScore(20, "bb_score_b");
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
        UpdateLines().Skip(1).Should().Equal("30=9", "30=10", "30=11 20=1");
        GameFacts().Should().BeEmpty("the achievements come when the game ends");
    }

    // Unlocked tiles closed in on their four sides by one team's locked tiles locked for it in
    // the update of the closing lock, a point each: [T8,4=8 SCORE_G=10 T7,4=8], 8 -> 10; 7,5
    // filled while the corner 8,6 was still unlocked (official client capture 2026-10-26,
    // session 012541 t 1130.0, 1860.5; 2/2).
    [Fact]
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
        Updates().Last().Should().BeEquivalentTo([(44, "8"), (47, "8")]);

        await Game.EndGameAsync(Ct);
        await RunFinishAsync();

        GameFacts()
            .Should()
            .Contain(
                (
                    100 + PLAYER,
                    AchievementSources.BATTLEBALL_TILE_LOCKED,
                    "battleballtileslocked",
                    2L
                )
            );
    }

    // results! (red) on the green gate: GATE_R 1 -> 0, effect 0, not counted green; the next gate
    // step joined (official client capture 2026-10-26, session 012541 t 1369.6-1409.0: 2/2
    // leave only, 2/2 join).
    [Fact]
    public async Task A_player_in_a_team_stepping_on_another_colours_gate_only_leaves()
    {
        var avatar = _room.Enter(PLAYER, 1, 1);
        AddGate(10, 3, 1, "bb_gate_r");
        AddGate(11, 3, 3, "bb_gate_g");
        await WalkToAsync(avatar, 3, 1);

        await WalkToAsync(avatar, 3, 3);

        Game.GetTeam((RoomObjectId)PLAYER).Should().Be(GameTeamType.None);
        avatar.EffectId.Should().Be(0);
        (StateOf(10), StateOf(11)).Should().Be(("0", "0"));

        await WalkToAsync(avatar, 3, 2);
        await WalkToAsync(avatar, 3, 3);

        Game.GetTeam((RoomObjectId)PLAYER).Should().Be(GameTeamType.Green);
        avatar.EffectId.Should().Be(34);
    }

    // While a game ran, Hutch. sent onto either gate from next to it did not move at all (official
    // client capture 2026-10-26, session 012541 t 1147-1196; 4/4).
    [Fact]
    public async Task Nobody_can_step_on_a_gate_while_a_game_runs()
    {
        var avatar = _room.Enter(PLAYER, 2, 1);
        AddGate(10, 3, 1, "bb_gate_g");
        await Game.StartGameAsync(Ct);

        await _room.Harness.Room.WalkAvatarToAsync(_room.ContextFor(avatar), 3, 1, Ct);
        await TickAvatarsAsync(avatar);

        (avatar.X, avatar.Y).Should().Be((2, 1));
        Game.GetTeam((RoomObjectId)PLAYER).Should().Be(GameTeamType.None);

        await Game.EndGameAsync(Ct);
        await WalkToAsync(avatar, 3, 1);

        Game.GetTeam((RoomObjectId)PLAYER).Should().Be(GameTeamType.Green);
    }

    // G3 (60 s, green 9 : red 0): Hutch. (green) waved ~0.5 s after the 0, the grey tiles went
    // dark a beat later, the green locked tiles flashed 0 / 8 ten times ending lit, red's and the
    // unlocked shades kept, no AvatarEffect, scoreboards kept; achievements at the end: the
    // player record +seconds (+60), tiles locked, GameAuthorExperience to the owner and
    // BattleBallWinner + GamePlayerExperience to the winner +players x seconds (+120) (official
    // client capture 2026-10-26, session 012541 t 1892.5-1898.5, 01-57-01-wait; finish 3/3,
    // achievements 2/2, winner 1/1).
    [Fact]
    public async Task A_game_that_ends_waves_its_winners_darkens_the_grey_tiles_and_flashes_the_winners_tiles()
    {
        var green = _room.Enter(PLAYER, 1, 1);
        var red = _room.Enter(SECOND, 1, 4);
        AddTile(30, 2, 1);
        AddTile(31, 2, 4);
        AddTile(32, 3, 4);
        AddTile(33, 6, 6);
        AddScore(20, "bb_score_g");
        await Game.JoinTeamAsync(
            (RoomObjectId)PLAYER,
            GameTeamType.Green,
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

        for (var step = 0; step < 3; step++)
        {
            await WalkToAsync(green, 2, 1);
            await WalkToAsync(green, 1, 1);
        }

        await WalkToAsync(red, 2, 4);
        await WalkToAsync(red, 3, 4);
        _room.Clock.AdvanceMs(60_000);
        var before = Updates().Count;

        await Game.EndGameAsync(Ct);
        await RunFinishAsync();

        _room
            .Harness.Fakes.SentToRoom<ExpressionMessageComposer>()
            .Select(x => ((int)x.ObjectId, x.ExpressionType))
            .Should()
            .Equal(((int)PLAYER, AvatarExpressionType.Wave));
        UpdateLines()
            .Skip(before)
            .Should()
            .Equal(
                "33=0",
                "30=0",
                "30=8",
                "30=0",
                "30=8",
                "30=0",
                "30=8",
                "30=0",
                "30=8",
                "30=0",
                "30=8"
            );
        (StateOf(31), StateOf(32), StateOf(20)).Should().Be(("3", "3", "1"));
        Game.GetTeam((RoomObjectId)SECOND).Should().Be(GameTeamType.Red);
        red.EffectId.Should().Be(33);
        GameFacts()
            .Should()
            .BeEquivalentTo([
                (100 + PLAYER, AchievementSources.ROOM_GAME_POINTS, "battleballplayer", 60L),
                (
                    100 + PLAYER,
                    AchievementSources.BATTLEBALL_TILE_LOCKED,
                    "battleballtileslocked",
                    1L
                ),
                (100 + PLAYER, AchievementSources.ROOM_GAME_POINTS, "battleballwinner", 120L),
                (100 + PLAYER, AchievementSources.ROOM_GAME_POINTS, "gameplayerexperience", 120L),
                (100 + SECOND, AchievementSources.ROOM_GAME_POINTS, "battleballplayer", 60L),
                (1, AchievementSources.ROOM_GAME_POINTS, "gameauthorexperience", 120L),
            ]);
    }

    // UseFurniture counter 2 while stopped: 30 -> 60 -> 120 -> 180 -> 300 -> 600 -> 30; after a
    // finished game (0) the first press showed the chosen 300 again, the next ones moved on
    // (official client capture 2026-10-26, session 012541 t 250.3-263.1, 1806.3-1821.0; 13/13
    // and 1/1).
    [Fact]
    public async Task The_counters_reset_goes_through_its_times_and_after_a_game_back_to_the_chosen_one()
    {
        var item = _room.AddFloorItem(
            50,
            8,
            1,
            FurnitureGameTimerLogic.LOGIC_NAME,
            className: "bb_counter",
            createLogic: (s, c) => new FurnitureGameTimerLogic(s, c)
        );
        var counter = (FurnitureGameTimerLogic)item.Logic;
        await counter.SetStateAsync(30);

        List<string> shown = [];

        for (var press = 0; press < 10; press++)
        {
            await counter.OnUseAsync(Owner, 2, Ct);
            shown.Add(StateOf(50));
        }

        shown.Should().Equal("60", "120", "180", "300", "600", "30", "60", "120", "180", "300");

        await counter.AdjustAsync(WiredOperatorType.Set, 0, Ct);

        await counter.OnUseAsync(Owner, 2, Ct);
        StateOf(50).Should().Be("300");
        await counter.OnUseAsync(Owner, 2, Ct);
        StateOf(50).Should().Be("600");
    }

    private IEnumerable<((int X, int Y) Item, FurnitureBattleBanzaiTileLogic Logic)> Tiles() =>
        _room
            .Harness.State.ItemsById.Values.OfType<IRoomFloorItem>()
            .Where(x => x.Logic is FurnitureBattleBanzaiTileLogic)
            .Select(x => ((x.X, x.Y), (FurnitureBattleBanzaiTileLogic)x.Logic))
            .ToList();

    private List<(int Player, string Source, string Value, long Amount)> GameFacts() =>
        [
            .. _room
                .Harness.Fakes.Log.Of("Record")
                .Select(x =>
                    (Player: ((PlayerId)x.Args[1]!).Value, Fact: (AchievementFact)x.Args[2]!)
                )
                .Where(x => x.Fact.Source.StartsWith("games.", StringComparison.Ordinal))
                .Select(x => (x.Player, x.Fact.Source, x.Fact.Value, (long)x.Fact.Amount)),
        ];

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    /// <summary>Runs the ended game's beats, one timer pass each.</summary>
    private async Task RunFinishAsync()
    {
        for (var beat = 0; beat < RoomGameSystem.END_BEATS + 2; beat++)
            await _room.Harness.Module<RoomTimerSystem>().ProcessTimersAsync(long.MaxValue / 2, Ct);
    }

    private void AddScore(int id, string className) =>
        _room.AddFloorItem(
            id,
            9,
            id - 19,
            FurnitureTeamScoreLogic.LOGIC_NAME,
            className: className,
            totalStates: 1,
            createLogic: (s, c) => new FurnitureTeamScoreLogic(s, c)
        );

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

    /// <summary>Each update as "id=state id=state".</summary>
    private List<string> UpdateLines() =>
        [.. Updates().Select(x => string.Join(" ", x.Select(s => $"{s.Item1}={s.Item2}")))];

    private List<List<(int, string)>> GateUpdates() =>
        [.. Updates().Where(x => x.Any(s => s.Item1 is >= 10 and < 20))];

    private async Task WalkToAsync(RoomPlayerAvatar avatar, int x, int y)
    {
        (await _room.Harness.Room.WalkAvatarToAsync(_room.ContextFor(avatar), x, y, Ct))
            .Should()
            .BeTrue();

        await TickAvatarsAsync(avatar);
    }

    private async Task TickAvatarsAsync(RoomPlayerAvatar avatar)
    {
        for (var tick = 0; tick < 30 && avatar.IsWalking; tick++)
        {
            _now += 10_000;
            await _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_now, Ct);
        }
    }
}
