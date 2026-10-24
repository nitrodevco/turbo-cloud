using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the triggering users stand on one of the picked furni: anywhere on a picked furni's
/// tile, above it in a stack included (official client capture 2026-10-24: picking the block
/// under the block the user stands on, or the wired box at the bottom, holds).
/// </summary>
[RoomObjectLogic("wf_cnd_trggrer_on_frn")]
public class WiredConditionExecutorOnItem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.TRIGGERER_IS_ON_FURNI;

    /// <summary>
    /// "All of the users match" (0) or "any of the users match" (1): the radio counts the users,
    /// not the furni. Habbo opens the box with quantifierType 2, and a user on one of two picked
    /// furni holds under both codes (official client capture 2026-10-24). The not twin shares it
    /// (inference, as the recorded twins of States Match and Stuff Type Matches do).
    /// </summary>
    protected override WiredQuantifierType QuantifierType => WiredQuantifierType.Users;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var selection = ctx.GetSelection(this);
        var tiles = new HashSet<int>();

        foreach (var item in GetFloorItems(selection))
        {
            if (FurniModule.GetTileIdForFloorItem(item, out var tileIds))
                tiles.UnionWith(tileIds);
        }

        if (tiles.Count == 0)
            return false;

        var players = GetPlayers(selection);

        if (players.Count == 0)
            return false;

        return Quantify(
            players.Select(p => tiles.Contains(MapModule.ToIdx(p.X, p.Y))),
            QuantifierRequiresAll
        );
    }
}
