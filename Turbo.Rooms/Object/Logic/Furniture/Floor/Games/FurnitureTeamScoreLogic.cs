using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Games;

/// <summary>
/// A team scoreboard (<c>bb_score_*</c>, <c>es_score_*</c>: Sulake's one
/// <c>TeamScoreCounterFurniture</c> for both games, its <c>customparams</c> the team). "Displays
/// the pink team's score" (furnidata <c>bb_score_r</c>): its state is the score, which the client
/// draws in four digits and counts up or down to (<c>furniture_score</c>,
/// <c>com/sulake/habbo/room/object/logic/furniture/_-M2C.as</c>, WIN63-202610201032). It follows
/// the room's team score, so wired Give Score counts on it as Banzai points do, and a game start
/// puts it back at 0.
/// </summary>
[RoomObjectLogic(LOGIC_NAME)]
public class FurnitureTeamScoreLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx),
        IRoomGameFurni
{
    public const string LOGIC_NAME = "game_team_score";

    private GameTeamType Team =>
        GameFurniture.TryParseScore(_ctx.Definition.Name, out var team) ? team : GameTeamType.None;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public async Task<bool> OnGameChangedAsync(RoomGameChange change, CancellationToken ct)
    {
        if (change is not (RoomGameChange.Started or RoomGameChange.Score))
            return false;

        if (Team == GameTeamType.None)
            return false;

        var score = GameSystem.GetScore(Team);

        if (GetState() == score)
            return false;

        await SetStateAsync(score, refresh: false);

        return true;
    }
}
