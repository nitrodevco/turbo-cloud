using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>True when the triggering users stand on one of the picked furni.</summary>
[RoomObjectLogic("wf_cnd_trggrer_on_frn")]
public class WiredConditionExecutorOnItem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.TRIGGERER_IS_ON_FURNI;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var selection = ctx.GetSelection(this);
        var tiles = new HashSet<int>();

        foreach (var item in GetFloorItems(selection))
        {
            if (_roomGrain.FurniModule.GetTileIdForFloorItem(item, out var tileIds))
                tiles.UnionWith(tileIds);
        }

        if (tiles.Count == 0)
            return false;

        var players = GetPlayers(selection);

        if (players.Count == 0)
            return false;

        return Quantify(
            players.Select(p => tiles.Contains(_roomGrain.MapModule.ToIdx(p.X, p.Y))),
            true
        );
    }
}
