using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Habbo's room branding furni: the room background (<c>ads_background</c>, client logic
/// <c>furniture_bg</c>) and the billboards (<c>ads_mpu_*</c>, <c>furniture_bb</c>). Map data
/// <c>state</c>, <c>imageUrl</c>, <c>clickUrl</c>, <c>offsetX</c>, <c>offsetY</c>,
/// <c>offsetZ</c>, which the client's <c>FurnitureRoomBrandingLogic</c> draws the image from and
/// staff edit in the info stand (<c>SetObjectData</c>). Nothing else: the client never uses it,
/// it drops the double-click. Its usage policy is the definition's even without states, as
/// Habbo sends a background with none as usable with rights.
/// </summary>
[RoomObjectLogic("room_branding")]
public class FurnitureRoomBrandingLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    protected override StuffDataType _stuffDataType => StuffDataType.MapKey;

    public override FurnitureUsageType GetUsagePolicy() => _ctx.Definition.UsagePolicy;
}
