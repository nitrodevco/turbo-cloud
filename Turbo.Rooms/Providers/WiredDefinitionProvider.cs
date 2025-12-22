using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Runtime;

namespace Turbo.Rooms.Providers;

public sealed class WiredDefinitionProvider(IServiceProvider host) : IWiredDefinitionProvider
{
    private readonly IServiceProvider _host = host;
    private readonly ConcurrentDictionary<string, WiredDefinitionReg> _defs = [];

    public IDisposable RegisterDefinition(
        string wiredType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomObjectContext, IWiredDefinition> factory
    )
    {
        var reg = new WiredDefinitionReg(sp, factory);

        _defs[wiredType] = reg;

        return new ActionDisposable(() =>
        {
            _defs.TryRemove(new KeyValuePair<string, WiredDefinitionReg>(wiredType, reg));
        });
    }

    public IWiredDefinition CreateWiredInstance(string wiredType, IRoomObjectContext ctx)
    {
        if (!_defs.TryGetValue(wiredType, out var reg))
            throw new TurboException(TurboErrorCodeEnum.InvalidWired);

        var sp = reg.ServiceProvider;

        if (sp != _host)
            sp = new CompositeServiceProvider(sp, _host);

        return reg.Factory(sp, ctx);
    }
}
