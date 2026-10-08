using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    /// <summary>
    /// Wired furni whose logic is not named after them, by the start of the classname: the chests
    /// (<c>wf_storage_furni1</c>, <c>_furni2</c>, <c>_furni_starter</c>...) and contracts, and the
    /// Ancient boxes, LTD copies of a box of ours.
    /// </summary>
    private static readonly (string Classname, string Logic)[] WIRED_CLASSNAME_LOGICS =
    [
        ("wf_storage_furni", "wired_chest_furni"),
        ("wf_storage_coins", "wired_chest_coins"),
        ("wf_contract_payment", "wired_contract_payment"),
        ("wf_contract_trade", "wired_contract_trade"),
        ("wf_contract_reward", "wired_contract_reward"),
        ("wf_proto_trg_at_given_time", "wf_trg_at_given_time"),
        ("wf_proto_cnd_trggrer_on_frn", "wf_cnd_trggrer_on_frn"),
        ("wf_ltdproto_act_toggle_state", "wf_act_toggle_state"),
    ];

    private readonly IServiceProvider _host = host;
    private readonly ILogger<IRoomObjectLogicProvider> _logger = logger;
    private readonly ConcurrentDictionary<string, RoomObjectLogicReg> _logics = [];
    private readonly RoomConfig _roomConfig = roomConfig.Value;

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
        if (
            logicType == DEFAULT_FLOOR_LOGIC
            && ctx.RoomObject is IRoomItem item
            && _roomConfig.WaterAreaLogicByDefinition.TryGetValue(
                item.Definition.Name,
                out var waterLogic
            )
        )
            logicType = waterLogic;

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

        // A highscore board is known by its classname (highscore_perteam*2 and so on).
        if (
            (logicType == DEFAULT_FLOOR_LOGIC || !_logics.ContainsKey(logicType))
            && ctx.RoomObject is IRoomItem board
            && HighscoreBoards.TryParse(board.Definition.Name, out _, out _)
        )
            logicType = HighscoreBoards.LOGIC_NAME;

        // A wired box's logic is its classname (or the one WIRED_CLASSNAME_LOGICS gives it). A
        // definition whose logic column names something else (left at the default, or a name
        // never registered) would make the box plain furniture that never opens its editor, so
        // the classname wins for wired boxes.
        if (
            (logicType == DEFAULT_FLOOR_LOGIC || !_logics.ContainsKey(logicType))
            && ctx.RoomObject is IRoomItem box
            && WiredLogicOf(box.Definition.Name) is { } wiredLogic
            && _logics.ContainsKey(wiredLogic)
        )
            logicType = wiredLogic;

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

    /// <summary>The logic a wired furni's classname stands for, or null for any other furni.</summary>
    private string? WiredLogicOf(string classname)
    {
        if (!classname.StartsWith(WIRED_CLASSNAME_PREFIX, StringComparison.Ordinal))
            return null;

        if (_logics.ContainsKey(classname))
            return classname;

        foreach (var (prefix, logic) in WIRED_CLASSNAME_LOGICS)
            if (classname.StartsWith(prefix, StringComparison.Ordinal))
                return logic;

        return null;
    }
}
