using Turbo.Primitives.Rooms.Object.Logic.Furniture;

namespace Turbo.Primitives.Rooms.Object.Furniture.Wall;

public interface IRoomWallItemContext<out TObject, out TLogic, out TSelf>
    : IRoomItemContext<TObject, TLogic, TSelf>
    where TObject : IRoomWallItem<TObject, TLogic, TSelf>
    where TSelf : IRoomWallItemContext<TObject, TLogic, TSelf>
    where TLogic : IFurnitureWallLogic<TObject, TLogic, TSelf>
{
    new TObject Object { get; }
}

public interface IRoomWallItemContext
    : IRoomItemContext<IRoomWallItem, IFurnitureWallLogic, IRoomWallItemContext> { }
