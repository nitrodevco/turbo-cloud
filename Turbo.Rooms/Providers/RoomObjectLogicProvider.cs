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
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Logic;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Games;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Highscore;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Runtime;

namespace Turbo.Rooms.Providers;

public sealed class RoomObjectLogicProvider(
    IServiceProvider host,
    ILogger<IRoomObjectLogicProvider> logger,
    IOptions<RoomConfig> roomConfig
) : IRoomObjectLogicProvider
{
    private const string DEFAULT_FLOOR_LOGIC = "default_floor";
    private const string WIRED_CLASSNAME_PREFIX = "wf_";

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
        if (
            logicType == DEFAULT_FLOOR_LOGIC
            && ctx.RoomObject is IRoomItem item
            && _roomConfig.WaterAreaLogicByDefinition.TryGetValue(
                item.Definition.Name,
                out var waterLogic
            )
        )
            logicType = waterLogic;

        // Habbo's recolorable furni (the 2026-10 building and alphabet blocks) are known by their
        // classnames, so a definition imported after MapRecolorableFurnitureLogic ran still keeps
        // a colour; their multi-height blocks step down in that logic too.
        if (
            logicType == DEFAULT_FLOOR_LOGIC
            && ctx.RoomObject is IRoomItem recolorable
            && RecolorableData.IsRecolorableClass(recolorable.Definition.Name)
        )
            logicType = RecolorableData.LOGIC_NAME;

        // A building block's height follows its state; its definition says so only through
        // its customparams (the step down per state), its logic column being the default.
        if (
            logicType == DEFAULT_FLOOR_LOGIC
            && ctx.RoomObject is IRoomItem block
            && MultiHeightFurniture.StepOf(block.Definition) is not null
        )
            logicType = MultiHeightFurniture.LOGIC_NAME;

        // The game timers (Banzai, Football, Freeze counters) are known by their classnames.
        if (
            (logicType == DEFAULT_FLOOR_LOGIC || !_logics.ContainsKey(logicType))
            && ctx.RoomObject is IRoomItem timer
            && Array.IndexOf(FurnitureGameTimerLogic.CLASSNAMES, timer.Definition.Name) >= 0
        )
            logicType = FurnitureGameTimerLogic.LOGIC_NAME;

        // So are the team gates, team scoreboards and Banzai tiles (GameFurniture).
        if (
            (logicType == DEFAULT_FLOOR_LOGIC || !_logics.ContainsKey(logicType))
            && ctx.RoomObject is IRoomItem gameFurni
            && GameFurniture.LogicOf(gameFurni.Definition.Name) is { } gameLogic
        )
            logicType = gameLogic;

        // Clothing furni are known by their furnidata specialtype (figure_purchasable_set), which
        // hotels set without a logic of their own.
        if (
            (logicType == DEFAULT_FLOOR_LOGIC || !_logics.ContainsKey(logicType))
            && ctx.RoomObject is IRoomItem clothing
            && clothing.Definition.FurniCategory == FurnitureCategory.FigurePurchasableSet
        )
            logicType = FurniturePurchasableClothingLogic.LOGIC_NAME;

        // A highscore board is known by its classname (highscore_perteam*2 and so on).
        if (
            (logicType == DEFAULT_FLOOR_LOGIC || !_logics.ContainsKey(logicType))
            && ctx.RoomObject is IRoomItem board
            && HighscoreBoards.TryParse(board.Definition.Name, out _, out _)
        )
            logicType = HighscoreBoards.LOGIC_NAME;

        // A wired box's logic is its classname. A definition whose logic column names something
        // else (left at the default, or a name never registered) would make the box plain
        // furniture that never opens its editor, so the classname wins for wired boxes.
        if (
            (logicType == DEFAULT_FLOOR_LOGIC || !_logics.ContainsKey(logicType))
            && ctx.RoomObject is IRoomItem box
            && box.Definition.Name.StartsWith(WIRED_CLASSNAME_PREFIX, StringComparison.Ordinal)
            && _logics.ContainsKey(box.Definition.Name)
        )
            logicType = box.Definition.Name;

        var fallback = !_logics.TryGetValue(logicType, out var reg);

        if (fallback)
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
