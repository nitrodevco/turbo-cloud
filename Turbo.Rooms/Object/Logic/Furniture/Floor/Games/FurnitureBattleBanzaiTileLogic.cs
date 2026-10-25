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
/// hof_furni 48082; frames 2 and 15 have no image). "For locking %limit% tiles in Battle Banzai"
/// (<c>badge_desc_ACH_BattleBallTilesLocked</c>) says a tile is locked.
/// Inference, not yet seen on Habbo: a game start lights every tile grey; a team player stepping
/// on a tile that is not yet theirs gives it their faint shade, each further step of theirs the
/// next, and the bright shade locks it for good with a point to their team; tiles closed in by
/// one team's locked tiles lock for that team too, a point each.
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

    private static readonly (int X, int Y)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>The state of a team's shade: 0 faint, 2 bright and locked.</summary>
    public static int StateOf(GameTeamType team, int shade) => (int)team * SHADES + shade;

    /// <summary>The team whose colour the tile shows; none while dark or grey.</summary>
    public GameTeamType Colour =>
        GetState() is >= SHADES and < ((int)GameTeamType.Yellow + 1) * SHADES
            ? (GameTeamType)(GetState() / SHADES)
            : GameTeamType.None;

    public bool IsLocked => Colour != GameTeamType.None && GetState() % SHADES == LOCKED_SHADE;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public async Task<bool> OnGameChangedAsync(RoomGameChange change, CancellationToken ct)
    {
        if (change != RoomGameChange.Started || GetState() == LIT_STATE)
            return false;

        await SetStateAsync(LIT_STATE, refresh: false);

        return true;
    }

    public override async Task OnWalkOnAsync(IRoomAvatarContext ctx, CancellationToken ct)
    {
        await base.OnWalkOnAsync(ctx, ct);

        if (!GameSystem.IsGameRunning || IsLocked)
            return;

        var team = GameSystem.GetTeam(ctx.ObjectId);

        if (team == GameTeamType.None)
            return;

        await SetStateAsync(Colour == team ? GetState() + 1 : StateOf(team, 0));

        if (IsLocked)
            await OnLockedAsync(team, ctx.ObjectId, ct);
    }

    private async Task OnLockedAsync(GameTeamType team, RoomObjectId scorerId, CancellationToken ct)
    {
        await GameSystem.GiveScoreAsync(team, POINTS_PER_TILE, _ctx.ObjectId, scorerId, 0, ct);

        var tiles = Tiles();
        var seen = new HashSet<(int, int)>();

        foreach (var (dx, dy) in Sides)
        {
            var start = (_ctx.RoomObject.X + dx, _ctx.RoomObject.Y + dy);

            if (seen.Contains(start) || !tiles.TryGetValue(start, out var first) || first.IsLocked)
                continue;

            var area = ClosedArea(start, team, tiles, seen);

            foreach (var tile in area)
            {
                await tile.SetStateAsync(StateOf(team, LOCKED_SHADE));
                await GameSystem.GiveScoreAsync(
                    team,
                    POINTS_PER_TILE,
                    tile._ctx.ObjectId,
                    scorerId,
                    0,
                    ct
                );
            }
        }
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
