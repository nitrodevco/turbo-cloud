using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired.Variables;
using Turbo.Runtime;

namespace Turbo.Rooms.Providers;

public sealed class RoomWiredVariablesProvider(IServiceProvider host) : IRoomWiredVariablesProvider
{
    private readonly IServiceProvider _host = host;
    private readonly List<WiredVariableReg> _variables = [];

    public IDisposable RegisterVariable(
        IServiceProvider sp,
        Func<IServiceProvider, IRoomGrain, IWiredVariable> factory
    )
    {
        var reg = new WiredVariableReg(sp, factory);

        _variables.Add(reg);

        return new ActionDisposable(() => _variables.Remove(reg));
    }

    public IEnumerable<IWiredVariable> BuildVariablesForRoom(IRoomGrain roomGrain)
    {
        foreach (var reg in _variables)
        {
            var sp = reg.ServiceProvider;

            if (sp != _host)
                sp = new CompositeServiceProvider(sp, _host);

            yield return reg.Factory(sp, roomGrain);
        }
    }
}
