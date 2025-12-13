using System;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Primitives.Rooms.Factories;

public interface IRoomObjectLogicFactory
{
    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomObjectContext, IRoomObjectLogic> factory
    );
    public IRoomObjectLogic CreateLogicInstance(string logicType, IRoomObjectContext ctx);
}
