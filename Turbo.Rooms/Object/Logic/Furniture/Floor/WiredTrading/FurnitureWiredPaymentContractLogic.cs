using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

/// <summary>A payment contract (<c>wf_contract_payment</c>).</summary>
[RoomObjectLogic("wired_contract_payment")]
public class FurnitureWiredPaymentContractLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredContractLogic(stuffDataFactory, ctx)
{
    public override WiredContractType ContractType => WiredContractType.Payment;
}
