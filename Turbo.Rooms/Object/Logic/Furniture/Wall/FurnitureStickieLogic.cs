using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Wall;

/// <summary>
/// A post-it note. Its legacy data is the note colour and text; double-clicking asks the server
/// for that data (<c>GetItemData</c>) rather than toggling anything, and edits arrive as
/// <c>SetItemData</c>.
/// </summary>
[RoomObjectLogic("stickie")]
public class FurnitureStickieLogic(IStuffDataFactory stuffDataFactory, IRoomWallItemContext ctx)
    : FurnitureWallLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;
}
