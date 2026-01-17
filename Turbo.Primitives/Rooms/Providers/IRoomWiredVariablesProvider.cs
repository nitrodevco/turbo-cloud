using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomWiredVariablesProvider
{
    public IDisposable RegisterVariable(
        IServiceProvider sp,
        Func<IServiceProvider, IRoomGrain, IWiredVariable> factory
    );
    public IEnumerable<IWiredVariable> BuildVariablesForRoom(IRoomGrain roomGrain);
}
