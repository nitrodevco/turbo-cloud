using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the triggering furni is of the same type as one of the furni picked on the box.
/// </summary>
[RoomObjectLogic("wf_cnd_stuff_is")]
public class WiredConditionItemPerType(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.STUFF_TYPE_MATCHES;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var wantedDefinitions = new HashSet<int>();

        foreach (var id in GetStuffIds())
        {
            if (TryGetFloorItem(id, out var picked))
                wantedDefinitions.Add(picked.Definition.Id);
        }

        if (wantedDefinitions.Count == 0)
            return false;

        var subjects = GetFloorItems(ctx.GetSelection(this));

        return Quantify(subjects.Select(x => wantedDefinitions.Contains(x.Definition.Id)), false);
    }
}
