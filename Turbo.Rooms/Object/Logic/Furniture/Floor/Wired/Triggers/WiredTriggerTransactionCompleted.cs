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

    // With no contract picked it never fires: neither for a Give Furni From Chest nor for a paid
    // payment contract (official client capture 2026-10-24 15:26Z, wired matrix trigger.25.R1,
    // 0/2); with the contract picked it fired on the payment (1/1).
    protected override bool FiresWithoutPicks => false;
}
