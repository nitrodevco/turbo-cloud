using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Games;

/// <summary>
/// The Battle Banzai tile, <c>bb_patch1</c> ("Banzai Arena Tile: Light them up to score points";
/// Sulake's <c>BattleBallFloorFurniture</c>, 16 states). Its asset draws state 0 dark, 1 lit
/// grey, and three shades for each team, faint to bright: 3-5 pink (red), 6-8 green, 9-11 blue,
/// 12-14 yellow (the mean colours of frames <c>bb_patch1_64_a_0_0</c> to <c>_14</c> of
/// hof_furni 48082; frames 2 and 15 have no image).
/// As Habbo plays it (official client capture 2026-10-26, session 012541, three games on a 4 x 4
/// field): a game start lights every tile grey in one update (3/3); a team player's step gives a
/// tile that is not yet theirs their faint shade, another team's unlocked shade included, each
/// further step of theirs the next, and the third, bright, locks it for good with a point to
/// their team in the same update (2/2 teams, 16 more locks); a locked tile never changes (4/4);
/// unlocked tiles closed in on their four sides by one team's locked tiles lock for that team in
/// the update of the lock that closes them, a point each (2/2). Every change goes out as an
/// <c>ObjectsDataUpdate</c>, in the millisecond the step onto the tile starts. When the game ends
/// the grey tiles go dark and the winning team's locked tiles flash
/// (<see cref="RoomGameChange.EndBeat"/>).
/// </summary>
[RoomObjectLogic(LOGIC_NAME)]
public class FurnitureBattleBanzaiTileLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx), IRoomGameFurni
{
    public const string LOGIC_NAME = "battlebanzai_tile";
    public const string CLASSNAME = "bb_patch1";

    public const int OFF_STATE = 0;
    public const int LIT_STATE = 1;

    private const int SHADES = 3;
    private const int LOCKED_SHADE = SHADES - 1;
    private const int POINTS_PER_TILE = 1;

    // The ended game's beats, 500 ms apart: the grey tiles went dark on the 2nd, the winners'
    // locked tiles went dark on the 3rd and lit and dark again on every beat after, ten updates
    // ending lit, while every other tile kept its state (official client capture 2026-10-26,
    // session 012541 t 1744.1-1750.1 and 1892.5-1898.5; t 1226.0-1232.1 the same ten under load;
    // 3/3).
    private const int DARKEN_BEAT = 2;
    private const int FLASH_BEAT = 3;

    private static readonly (int X, int Y)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>A change set without telling the room, which goes out with the game's next update.</summary>
    private bool _pending;

    /// <summary>The state the tile was in when the last game ended.</summary>
    private int _endState;

    /// <summary>The state of a team's shade: 0 faint, 2 bright and locked.</summary>
    public static int StateOf(GameTeamType team, int shade) => (int)team * SHADES + shade;

    /// <summary>The team whose colour the tile shows; none while dark or grey.</summary>
    public GameTeamType Colour => ColourOf(GetState());

    public bool IsLocked => IsLockedState(GetState());

    private static GameTeamType ColourOf(int state) =>
        state is >= SHADES and < ((int)GameTeamType.Yellow + 1) * SHADES
            ? (GameTeamType)(state / SHADES)
            : GameTeamType.None;

    private static bool IsLockedState(int state) =>
        ColourOf(state) != GameTeamType.None && state % SHADES == LOCKED_SHADE;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public async Task<bool> OnGameChangedAsync(RoomGameChange change, CancellationToken ct)
    {
        switch (change)
        {
            case RoomGameChange.Started:
                _pending = false;

                return await ShowAsync(LIT_STATE);
            case RoomGameChange.Ended:
                _endState = GetState();

                return false;
            case RoomGameChange.EndBeat:
                return await ShowEndBeatAsync();
        }

        if (!_pending)
            return false;

        _pending = false;

        return true;
    }

    private Task<bool> ShowEndBeatAsync()
    {
        var beat = GameSystem.EndBeat;

        if (beat == DARKEN_BEAT && _endState == LIT_STATE)
            return ShowAsync(OFF_STATE);

        if (
            beat < FLASH_BEAT
            || !IsLockedState(_endState)
            || ColourOf(_endState) != GameSystem.WinningTeam
        )
            return Task.FromResult(false);

        return ShowAsync((beat - FLASH_BEAT) % 2 == 0 ? OFF_STATE : _endState);
    }

    private async Task<bool> ShowAsync(int state)
    {
        if (GetState() == state)
            return false;

        await SetStateAsync(state, refresh: false);

        return true;
    }

    public override async Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOnAsync(ctx, ct);

        await StepAsync(ctx.ObjectId, ct);

        await GameSystem.SendGameFurniAsync(ct);
    }

    /// <summary>
    /// A step of <paramref name="avatarId"/>'s team on the tile, by their foot or by the puck they
    /// kicked; the change waits for the game's next update.
    /// </summary>
    public async Task StepAsync(RoomObjectId avatarId, CancellationToken ct)
    {
        if (!GameSystem.IsGameRunning || IsLocked)
            return;

        var team = GameSystem.GetTeam(avatarId);

        if (team == GameTeamType.None)
            return;

        await ColourAsync(Colour == team ? GetState() + 1 : StateOf(team, 0));

        if (IsLocked)
            await OnLockedAsync(team, avatarId, ct);
    }

    private async Task ColourAsync(int state)
    {
        await SetStateAsync(state, refresh: false);

        _pending = true;
    }

    /// <summary>
    /// The tile locked for <paramref name="team"/>: it and the tiles it closes in score together,
    /// and count towards the player's tiles locked when the game ends.
    /// </summary>
    private async Task OnLockedAsync(GameTeamType team, RoomObjectId scorerId, CancellationToken ct)
    {
        var tiles = Tiles();
        var seen = new HashSet<(int, int)>();
        var locked = 1;

        foreach (var (dx, dy) in Sides)
        {
            var start = (_ctx.RoomObject.X + dx, _ctx.RoomObject.Y + dy);

            if (seen.Contains(start) || !tiles.TryGetValue(start, out var first) || first.IsLocked)
                continue;

            var area = ClosedArea(start, team, tiles, seen);

            locked += area.Count;

            foreach (var tile in area)
                await tile.ColourAsync(StateOf(team, LOCKED_SHADE));
        }

        GameSystem.CountTilesLocked(scorerId, locked);

        await GameSystem.GiveScoreAsync(
            team,
            locked * POINTS_PER_TILE,
            _ctx.ObjectId,
            scorerId,
            0,
            ct
        );
    }

    /// <summary>
    /// The unlocked tiles joined to <paramref name="start"/>, when <paramref name="team"/>'s locked
    /// tiles close them in on every side; none when they reach a square without a tile or another
    /// team's locked tile.
    /// </summary>
    private static List<FurnitureBattleBanzaiTileLogic> ClosedArea(
        (int X, int Y) start,
        GameTeamType team,
        Dictionary<(int, int), FurnitureBattleBanzaiTileLogic> tiles,
        HashSet<(int, int)> seen
    )
    {
        var area = new List<FurnitureBattleBanzaiTileLogic>();
        var queue = new Queue<(int X, int Y)>([start]);
        var closed = true;

        seen.Add(start);

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();

            area.Add(tiles[at]);

            foreach (var (dx, dy) in Sides)
            {
                var next = (at.X + dx, at.Y + dy);

                if (!tiles.TryGetValue(next, out var tile))
                {
                    closed = false;
                    continue;
                }

                if (tile.IsLocked)
                {
                    if (tile.Colour != team)
                        closed = false;

                    continue;
                }

                if (seen.Add(next))
                    queue.Enqueue(next);
            }
        }

        return closed ? area : [];
    }

    private Dictionary<(int, int), FurnitureBattleBanzaiTileLogic> Tiles()
    {
        var tiles = new Dictionary<(int, int), FurnitureBattleBanzaiTileLogic>();

        foreach (var item in FurniModule.Items.OfType<IRoomFloorItem>())
        {
            if (item.Logic is FurnitureBattleBanzaiTileLogic tile)
                tiles.TryAdd((item.X, item.Y), tile);
        }

        return tiles;
    }
}
