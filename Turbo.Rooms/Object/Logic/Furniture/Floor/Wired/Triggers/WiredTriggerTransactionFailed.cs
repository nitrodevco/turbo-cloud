using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>Fires when a user's transaction did not go through, whatever the reason.</summary>
[RoomObjectLogic("wf_trg_transaction_fail")]
public class WiredTriggerTransactionFailed(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
)
    : FurnitureWiredTransactionTriggerLogic<WiredTransactionFailedEvent>(
        grainFactory,
        stuffDataFactory,
        ctx
    )
{
    public override int WiredCode => (int)WiredTriggerType.TRANSACTION_FAILED;

    protected override RoomObjectId SourceOf(WiredTransactionFailedEvent evt) => evt.SourceId;
}
