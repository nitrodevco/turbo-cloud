using System;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomObjectLogicProvider
{
    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomObjectContext, IRoomObjectLogic> factory
    );
    public IRoomObjectLogic CreateLogicInstance(string logicType, IRoomObjectContext ctx);
}
