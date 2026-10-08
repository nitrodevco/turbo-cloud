using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A badge display. Its data is the string array <see cref="BadgeDisplayData"/> describes,
/// written when it is bought; the client draws the badge and opens the engraving on a
/// double-click without asking the server, and nothing on it changes afterwards.
/// </summary>
[RoomObjectLogic(BadgeDisplayData.LOGIC_NAME)]
public class FurnitureBadgeDisplayLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    protected override StuffDataType _stuffDataType => StuffDataType.StringKey;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;
}
