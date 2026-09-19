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

/// <summary>True when avatars stand on the picked furni (all of them when param 0 is set).</summary>
[RoomObjectLogic("wf_cnd_furnis_hv_avtrs")]
public class WiredConditionItemHasHabbo(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.FURNIS_HAVE_AVATARS;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var items = GetFloorItems(ctx.GetSelection(this));

        return Quantify(items.Select(HasAvatar), RequiresAll());
    }

    private bool HasAvatar(IRoomFloorItem item)
    {
        if (!_roomGrain.FurniModule.GetTileIdForFloorItem(item, out var tileIds))
            return false;

        return tileIds.Any(tileId =>
            _roomGrain.MapModule.InBounds(tileId)
            && _roomGrain._state.TileAvatarStacks[tileId].Count > 0
        );
    }
}
