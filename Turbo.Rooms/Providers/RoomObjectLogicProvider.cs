using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Logic;
using Turbo.Runtime;

namespace Turbo.Rooms.Providers;

public sealed class RoomObjectLogicProvider(
    IServiceProvider host,
    ILogger<IRoomObjectLogicProvider> logger,
    IOptions<RoomConfig> roomConfig
) : IRoomObjectLogicProvider
{
    private readonly IServiceProvider _host = host;
    private readonly IServiceProvider _hostForPlugins = new PluginHostServiceProvider(host);
    private readonly ILogger<IRoomObjectLogicProvider> _logger = logger;
    private readonly ConcurrentDictionary<string, RoomObjectLogicReg> _logics = [];
    private readonly RoomConfig _roomConfig = roomConfig.Value;

    // The registration each logic was made by, and the type it was made for.
    private readonly ConditionalWeakTable<IRoomObjectLogic, Made> _made = [];
    private int _version;

    public IReadOnlyList<string> LogicTypes => [.. _logics.Keys.Order(StringComparer.Ordinal)];

    public int Version => Volatile.Read(ref _version);

    public bool IsCurrent(IRoomObjectLogic logic) =>
        !_made.TryGetValue(logic, out var made)
        || (
            _logics.TryGetValue(made.LogicType, out var reg)
                ? ReferenceEquals(reg, made.Reg)
                : made.Fallback
        );

    public IDisposable RegisterLogic(
        string logicType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomObjectContext, IRoomObjectLogic> factory
    )
    {
        var reg = new RoomObjectLogicReg(sp, factory);

        _logics[logicType] = reg;
        Interlocked.Increment(ref _version);

        return new ActionDisposable(() =>
        {
            if (_logics.TryRemove(new KeyValuePair<string, RoomObjectLogicReg>(logicType, reg)))
                Interlocked.Increment(ref _version);
        });
    }

    public IRoomObjectLogic CreateLogicInstance(string logicType, IRoomObjectContext ctx)
    {
        if (ctx.RoomObject is IRoomItem item)
            logicType = RoomObjectLogics.Resolve(
                logicType,
                item.Definition,
                _logics.ContainsKey,
                _roomConfig.WaterAreaLogicByDefinition
            );

        var fallback = !_logics.TryGetValue(logicType, out var reg);

        if (fallback)
        {
            // An unknown logic type still gets a working item; the warning is what tells us a
            // definition asks for behaviour the server has not implemented yet.
            _logger.LogWarning(
                "Logic type {LogicType} is not registered; object {ObjectId} falls back to {Fallback}",
                logicType,
                ctx.ObjectId,
                DefaultFurnitureLogic.FLOOR
            );

            reg = _logics.TryGetValue(DefaultFurnitureLogic.FLOOR, out var defaultReg)
                ? defaultReg
                : null;
        }

        if (reg is null)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        var sp = reg.ServiceProvider;

        if (sp != _host)
            sp = new CompositeServiceProvider(sp, _hostForPlugins);

        var logic = reg.Factory(sp, ctx);

        _made.AddOrUpdate(logic, new Made(logicType, reg, fallback));

        return logic;
    }

    /// <summary>
    /// What made a logic: the registration and the type it was asked for; a fallback was made by
    /// the default's registration while that type had none.
    /// </summary>
    private sealed record Made(string LogicType, RoomObjectLogicReg Reg, bool Fallback);
}
