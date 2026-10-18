using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Moves the picked furni onto the tile of the first selected user.</summary>
[RoomObjectLogic("wf_act_furni_to_user")]
public class WiredActionFurniToUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredMoveActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.MOVE_FURNI_TO_USER;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override void PlanMoves(IWiredContext ctx, WiredMovePlan plan)
    {
        var selection = ctx.GetSelection(this);
        var players = GetPlayers(selection);
        var items = GetFloorItems(selection);

        if (players.Count == 0 || items.Count == 0)
            return;

        var target = players[0];

        foreach (var item in items)
            plan.Move(item, target.X, target.Y);
    }
}
