using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Runtime;

namespace Turbo.Rooms.Furniture.Logic;

public sealed class FurnitureLogicFactory(IServiceProvider host) : IFurnitureLogicFactory
{
    private readonly IServiceProvider _host = host;
    private readonly ConcurrentDictionary<string, FurnitureLogicReg> _logics = [];

    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, object> activator
    )
    {
        var reg = new FurnitureLogicReg(sp, activator);

        _logics[logicType] = reg;

        return new ActionDisposable(() =>
        {
            _logics.TryRemove(new KeyValuePair<string, FurnitureLogicReg>(logicType, reg));
        });
    }

    public IFurnitureLogic CreateLogicInstance(string logicType)
    {
        IServiceScope scope = _host.CreateAsyncScope();

        if (!_logics.TryGetValue(logicType, out var reg))
        {
            reg = default!;
        }

        try
        {
            if (scope != reg.ServiceProvider)
            {
                Console.WriteLine("need joined scope");
                scope = new JoinedScope(scope, reg.ServiceProvider.CreateAsyncScope());
            }

            try
            {
                return (IFurnitureLogic)reg.Activator(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        catch
        {
            throw;
        }
    }
}
