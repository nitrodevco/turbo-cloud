using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>True when other furni are stacked on the picked furni (all of them when param 0 is set).</summary>
[RoomObjectLogic("wf_cnd_has_furni_on")]
public class WiredConditionItemHasItems(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.HAS_STACKED_FURNIS;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var items = GetFloorItems(ctx.GetSelection(this));

        return Quantify(items.Select(HasItemOnTop), RequiresAll());
    }

    private bool HasItemOnTop(IRoomFloorItem item)
    {
        if (!_roomGrain.FurniModule.GetTileIdForFloorItem(item, out var tileIds))
            return false;

        foreach (var tileId in tileIds)
        {
            if (!_roomGrain.MapModule.InBounds(tileId))
                continue;

            foreach (var otherId in _roomGrain._state.TileFloorStacks[tileId])
            {
                if (otherId == item.ObjectId)
                    continue;

                if (TryGetFloorItem(otherId, out var other) && other.Z > item.Z)
                    return true;
            }
        }

        return false;
    }
}
