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
/// "Furni Matches": true when the furni to match (slot 0, the triggering furni by default) are
/// among the furni to compare with (slot 1, the picks by default): <c>sources.furni.title.match.0/1</c>
/// (AS3 <c>StuffTypeMatches.furniSelectionTitle</c>), official client capture of the box's sources.
/// It compares the furni themselves, not their kind: on Habbo a furni matched itself 4 of 4 times
/// and another furni of the same kind, colour, state and direction 0 of 8 times (official client
/// capture 2026-10-24, wired lab, build WIN63-202610201032).
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

    /// <summary>
    /// "All of the furni match" (0) or "any of the furni match" (1) over the furni to match;
    /// Habbo opens the box and its negative twin with quantifierType 1 (official client capture
    /// of codes 8 and 19).
    /// </summary>
    protected override WiredQuantifierType QuantifierType => WiredQuantifierType.Furni;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.TriggeredItem),
            WiredSources.FurniOrSecondary.DefaultingTo(WiredFurniSourceType.SelectedItems),
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var compareWith = Existing(WiredSlotSelection.ForSlot(this, ctx, 1)).ToHashSet();

        if (compareWith.Count == 0)
            return false;

        // No furni to match (a trigger without one): "all of the furni match" holds and "any"
        // does not, as Quantify folds an empty set (official client capture 2026-10-24, under
        // Periodically, 6 of 6 each).
        return Quantify(
            Existing(WiredSlotSelection.ForSlot(this, ctx, 0)).Select(compareWith.Contains),
            QuantifierRequiresAll
        );
    }

    private IEnumerable<int> Existing(IWiredSelectionSet selection) =>
        selection.SelectedFurniIds.Where(id => FurniModule.TryGetItem(id, out _));
}
