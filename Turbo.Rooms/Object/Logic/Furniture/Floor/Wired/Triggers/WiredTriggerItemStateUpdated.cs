using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

[RoomObjectLogic("wf_trg_state_changed")]
public class WiredTriggerItemStateUpdated(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
)
    : FurnitureWiredFurniEventTriggerLogic<RoomItemStateChangedEvent>(
        grainFactory,
        stuffDataFactory,
        ctx
    )
{
    public override int WiredCode => (int)WiredTriggerType.STATE_CHANGE;

    /// <summary>
    /// The editor's "trigger for all states" (0) or "for the current state" (1). The server
    /// does not honour 1 yet and fires on every change; the rule stays so the editor gets back
    /// the choice it saved.
    /// </summary>
    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    protected override RoomObjectId GetFurniId(RoomItemStateChangedEvent evt) => evt.ObjectId;
}
