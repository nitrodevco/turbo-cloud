using System;

namespace Turbo.Primitives.Rooms.Object.Logic;

public interface IRoomObjectLogicFactory
{
    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomObjectContext, IRoomObjectLogic> factory
    );
    public IRoomObjectLogic CreateLogicInstance(string logicType, IRoomObjectContext ctx);
}
