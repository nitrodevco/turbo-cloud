using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
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
    /// The box remembers the states its picked furni were in when it was saved (or when "apply
    /// snapshot" was pressed): what "trigger for the current state" compares with.
    /// </summary>
    protected override bool KeepsFurniSnapshot => true;

    /// <summary>
    /// The editor's "trigger for all states" (0, <c>wiredfurni.params.state_trigger.0</c>) or "for
    /// the current state" (1): only when a picked furni changes to the state it had in the box's
    /// snapshot.
    /// </summary>
    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override async Task<bool> MatchesEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (!await base.MatchesEventAsync(evt, ct) || evt is not RoomItemStateChangedEvent changed)
            return false;

        if (!GetIntParamOrDefault(0, false))
            return true;

        var furniId = changed.ObjectId.Value;

        return GetFurniSnapshot().TryGetValue(furniId, out var snapshot)
            && TryGetFloorItem(furniId, out var item)
            && item.Logic.GetState() == snapshot.State;
    }

    protected override RoomObjectId GetFurniId(RoomItemStateChangedEvent evt) => evt.ObjectId;
}
