using System;

namespace Turbo.Primitives.Rooms.Furniture.Logic;

public interface IFurnitureLogicFactory
{
    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomItemContext, IFurnitureLogic> factory
    );
    public IFurnitureLogic CreateLogicInstance(string logicType, IRoomItemContext ctx);
}
