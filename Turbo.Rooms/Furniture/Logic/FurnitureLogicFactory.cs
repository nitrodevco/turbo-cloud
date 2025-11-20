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
        if (!_logics.TryGetValue(logicType, out var reg))
            throw new InvalidOperationException(
                $"No furniture logic registered for type '{logicType}'"
            );

        try
        {
            var sp = reg.ServiceProvider;

            if (sp != _host)
                sp = new CompositeServiceProvider(sp, _host);

            try
            {
                return (IFurnitureLogic)reg.Activator(sp);
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
