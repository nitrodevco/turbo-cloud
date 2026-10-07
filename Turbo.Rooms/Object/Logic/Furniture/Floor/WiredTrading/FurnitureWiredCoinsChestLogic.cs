using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

/// <summary>A chest of credits (<c>wf_storage_coins*</c>), filled with credit furni.</summary>
[RoomObjectLogic("wired_chest_coins")]
public class FurnitureWiredCoinsChestLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredChestLogic(stuffDataFactory, ctx)
{
    public override WiredChestKind Kind => WiredChestKind.Coins;
}
