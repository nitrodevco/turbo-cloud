using Turbo.Primitives.Rooms.Object.Furniture.Wall;

namespace Turbo.Primitives.Rooms.Object.Logic.Furniture;

public interface IFurnitureWallLogic<out TObject, out TLogic, out TContext>
    : IFurnitureLogic<TObject, TLogic, TContext>
    where TObject : IRoomWallItem<TObject, TLogic, TContext>
    where TContext : IRoomWallItemContext<TObject, TLogic, TContext>
    where TLogic : IFurnitureWallLogic<TObject, TLogic, TContext>
{
    new TContext Context { get; }
}

public interface IFurnitureWallLogic
    : IFurnitureLogic<IRoomWallItem, IFurnitureWallLogic, IRoomWallItemContext> { }
