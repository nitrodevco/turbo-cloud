using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomWiredVariablesProvider
{
    public IDisposable RegisterVariable(
        string key,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomGrain, IWiredVariable> factory
    );
    public IEnumerable<IWiredVariable> BuildAllVariables(IRoomGrain roomGrain);
}
