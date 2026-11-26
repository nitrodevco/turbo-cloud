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
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

[RoomObjectLogic("wf_slc_furni_onfurni")]
public class WiredSelectorItemsOnItem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.FURNI_ON_FURNI;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredFurniSelectionType>(WiredFurniSelectionType.FurniAboveFurni)];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var input = ctx.GetSelection(this);
        var mode = GetIntParamOrDefault(0, WiredFurniSelectionType.FurniAboveFurni);
        var output = new WiredSelectionSet();

        foreach (var id in input.SelectedFurniIds)
        {
            // Every tile the furni covers, as "Furni Has Furni On" and "Users On Furni" read it:
            // only its origin tile used to be looked at, so what stood on the rest of a sofa was
            // never found (inferred from those two; not recorded with a furni wider than a tile).
            if (
                !FurniModule.TryGetFloorItem(id, out var floorItem)
                || !FurniModule.GetTileIdForFloorItem(floorItem, out var tileIds)
            )
                continue;

            foreach (var tileIdx in tileIds)
            {
                foreach (var other in FurniModule.GetFloorItemsOnTile(tileIdx))
                {
                    // "Furni at same height" (wiredfurni.params.onfurni.2) picks the picked furni
                    // itself too: a pile's bottom box and its 4th box each selected themselves,
                    // with furni on top of both (official client capture 2026-10-24, W4 lab
                    // selector.4.R5, 1/1 each).
                    var matches = mode switch
                    {
                        WiredFurniSelectionType.FurniAboveFurni => other.Z > floorItem.Z,
                        WiredFurniSelectionType.FurniUnderFurni => other.Z < floorItem.Z,
                        WiredFurniSelectionType.FurniHeightMatches => other.Z == floorItem.Z,
                        WiredFurniSelectionType.AllFurniOnTile => true,
                        _ => false,
                    };

                    if (matches)
                        output.SelectedFurniIds.Add((int)other.ObjectId);
                }
            }
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
