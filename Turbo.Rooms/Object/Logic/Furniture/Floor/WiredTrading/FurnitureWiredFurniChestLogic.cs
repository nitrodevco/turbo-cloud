using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;

/// <summary>A chest of furni (<c>wf_storage_furni*</c>).</summary>
[RoomObjectLogic("wired_chest_furni")]
public class FurnitureWiredFurniChestLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredChestLogic(stuffDataFactory, ctx)
{
    public override WiredChestKind Kind => WiredChestKind.Furni;
}
