using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Object.Logic;
using Turbo.Runtime;

namespace Turbo.Rooms.Providers;

public sealed class RoomObjectLogicProvider(IServiceProvider host) : IRoomObjectLogicProvider
{
    private readonly IServiceProvider _host = host;
    private readonly ConcurrentDictionary<string, RoomObjectLogicReg> _logics = [];

    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomObjectContext, IRoomObjectLogic> factory
    )
    {
        var reg = new RoomObjectLogicReg(sp, factory);

        _logics[logicType] = reg;

        return new ActionDisposable(() =>
        {
            _logics.TryRemove(new KeyValuePair<string, RoomObjectLogicReg>(logicType, reg));
        });
    }

    public IRoomObjectLogic CreateLogicInstance(string logicType, IRoomObjectContext ctx)
    {
        if (!_logics.TryGetValue(logicType, out var reg))
        {
            Console.WriteLine(
                $"[RoomObjectLogicProvider] Logic type '{logicType}' not found, falling back to default_floor"
            );
            reg = _logics.TryGetValue("default_floor", out var defaultReg) ? defaultReg : null;
        }

        if (reg is null)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        // TODO we need to fall back if not found

        var sp = reg.ServiceProvider;

        if (sp != _host)
            sp = new CompositeServiceProvider(sp, _host);

        return reg.Factory(sp, ctx);
    }
}
