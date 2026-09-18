using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Object.Logic;
using Turbo.Runtime;

namespace Turbo.Rooms.Providers;

public sealed class RoomObjectLogicProvider(
    IServiceProvider host,
    ILogger<IRoomObjectLogicProvider> logger
) : IRoomObjectLogicProvider
{
    private const string DEFAULT_FLOOR_LOGIC = "default_floor";

    private readonly IServiceProvider _host = host;
    private readonly ILogger<IRoomObjectLogicProvider> _logger = logger;
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
            // An unknown logic type still gets a working item; the warning is what tells us a
            // definition asks for behaviour the server has not implemented yet.
            _logger.LogWarning(
                "Logic type {LogicType} is not registered; object {ObjectId} falls back to {Fallback}",
                logicType,
                ctx.ObjectId,
                DEFAULT_FLOOR_LOGIC
            );

            reg = _logics.TryGetValue(DEFAULT_FLOOR_LOGIC, out var defaultReg) ? defaultReg : null;
        }

        if (reg is null)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        var sp = reg.ServiceProvider;

        if (sp != _host)
            sp = new CompositeServiceProvider(sp, _host);

        return reg.Factory(sp, ctx);
    }
}
