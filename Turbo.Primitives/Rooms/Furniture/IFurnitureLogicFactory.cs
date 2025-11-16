using System;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IFurnitureLogicFactory
{
    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, object> activator
    );
    public IFurnitureLogic CreateLogicInstance(string logicType);
}
