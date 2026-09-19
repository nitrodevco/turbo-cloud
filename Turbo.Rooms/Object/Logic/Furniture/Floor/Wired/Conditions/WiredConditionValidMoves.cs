using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>True when the picked furni have at least one neighbouring tile they could move to.</summary>
[RoomObjectLogic("wf_cnd_valid_moves")]
public class WiredConditionValidMoves(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.CAN_PERFORM_MOVE;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var items = GetFloorItems(ctx.GetSelection(this));

        return Quantify(items.Select(CanMoveAnywhere), true);
    }

    private bool CanMoveAnywhere(IRoomFloorItem item)
    {
        var map = _roomGrain.MapModule;
        var idx = map.ToIdx(item.X, item.Y);

        foreach (var direction in RotationExtensions.CARDINAL)
        {
            if (!map.TryGetTileInFront(idx, direction, out var nextIdx))
                continue;

            var (x, y) = map.GetTileXY(nextIdx);

            if (_roomGrain.FurniModule.CanPlaceFloorItem(item.ObjectId, x, y, item.Rotation))
                return true;
        }

        return false;
    }
}
