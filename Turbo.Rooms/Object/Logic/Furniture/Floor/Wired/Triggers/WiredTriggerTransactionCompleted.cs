using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>Fires when a user completed a transaction: paid, traded or was rewarded.</summary>
[RoomObjectLogic("wf_trg_transaction_complete")]
public class WiredTriggerTransactionCompleted(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
)
    : FurnitureWiredTransactionTriggerLogic<WiredTransactionCompletedEvent>(
        grainFactory,
        stuffDataFactory,
        ctx
    )
{
    public override int WiredCode => (int)WiredTriggerType.TRANSACTION_COMPLETED;

    protected override RoomObjectId SourceOf(WiredTransactionCompletedEvent evt) => evt.SourceId;
}
