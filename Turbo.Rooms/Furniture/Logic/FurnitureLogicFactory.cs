using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Turbo.Contracts.Enums;
using Turbo.Logging;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Runtime;

namespace Turbo.Rooms.Furniture.Logic;

public sealed class FurnitureLogicFactory(IServiceProvider host) : IFurnitureLogicFactory
{
    private readonly IServiceProvider _host = host;
    private readonly ConcurrentDictionary<string, FurnitureLogicReg> _logics = [];

    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomItemContext, IFurnitureLogic> factory
    )
    {
        var reg = new FurnitureLogicReg(sp, factory);

        _logics[logicType] = reg;

        return new ActionDisposable(() =>
        {
            _logics.TryRemove(new KeyValuePair<string, FurnitureLogicReg>(logicType, reg));
        });
    }

    public IFurnitureLogic CreateLogicInstance(string logicType, IRoomItemContext ctx)
    {
        if (!_logics.TryGetValue(logicType, out var reg))
            throw new TurboException(TurboErrorCodeEnum.LogicNotFound);

        var sp = reg.ServiceProvider;

        if (sp != _host)
            sp = new CompositeServiceProvider(sp, _host);

        return reg.Factory(sp, ctx);
    }
}
