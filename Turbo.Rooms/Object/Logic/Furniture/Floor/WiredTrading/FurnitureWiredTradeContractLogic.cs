using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

/// <summary>A trade contract (<c>wf_contract_trade</c>).</summary>
[RoomObjectLogic("wired_contract_trade")]
public class FurnitureWiredTradeContractLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredContractLogic(stuffDataFactory, ctx)
{
    public override WiredContractType ContractType => WiredContractType.Trade;
}
