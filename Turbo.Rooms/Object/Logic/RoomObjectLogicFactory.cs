using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Runtime;

namespace Turbo.Rooms.Object.Logic;

public sealed class RoomObjectLogicFactory(IServiceProvider host) : IRoomObjectLogicFactory
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
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        var sp = reg.ServiceProvider;

        if (sp != _host)
            sp = new CompositeServiceProvider(sp, _host);

        return reg.Factory(sp, ctx);
    }
}
