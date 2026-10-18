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
/// True when the furni to match (slot 0, the triggering furni by default) are of the type of one
/// of the furni to compare with (slot 1, the picks by default): <c>sources.furni.title.match.0/1</c>
/// (AS3 <c>StuffTypeMatches.furniSelectionTitle</c>), official client capture of the box's sources.
/// </summary>
[RoomObjectLogic("wf_cnd_stuff_is")]
public class WiredConditionItemPerType(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.STUFF_TYPE_MATCHES;

    protected override bool AllowsWallFurni => true;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.TriggeredItem),
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SelectedItems),
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var wantedDefinitions = new HashSet<int>();

        foreach (var id in WiredSlotSelection.ForSlot(this, ctx, 1).SelectedFurniIds)
        {
            if (TryGetFloorItem(id, out var picked))
                wantedDefinitions.Add(picked.Definition.Id);
        }

        if (wantedDefinitions.Count == 0)
            return false;

        var subjects = GetFloorItems(WiredSlotSelection.ForSlot(this, ctx, 0));

        return Quantify(subjects.Select(x => wantedDefinitions.Contains(x.Definition.Id)), false);
    }
}
