using System;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Wired.Leaderboard;
using Turbo.Rooms.Wired.VariableFx;

namespace Turbo.Rooms.Grains;

/// <summary>
/// Anything that lives inside one room grain and works through its siblings: the modules, the
/// systems, and every object logic. It names the siblings the way the grain's own partials do,
/// so a call reads <c>ObjectModule.RemoveObjectAsync(...)</c> rather than
/// <c>_roomGrain.ObjectModule.RemoveObjectAsync(...)</c>. The grain's live state, logger, grain
/// factory, room and wired configs and clock are named the same way (<see cref="State"/>,
/// <see cref="Logger"/>, <see cref="NowMs"/>, ...); the grain itself stays reachable as
/// <see cref="_roomGrain"/> for its other services.
/// <para>
/// A new module or system on <see cref="RoomGrain"/> gets its shorthand here as well; this is
/// the one list, so nothing else keeps its own.
/// </para>
/// </summary>
public abstract class RoomGrainComponent(RoomGrain roomGrain)
{
    protected readonly RoomGrain _roomGrain = roomGrain;

    // private protected: the grain's fields are internal, and RoomLiveState is internal too.
    private protected RoomLiveState State => _roomGrain._state;
    private protected ILogger<IRoomGrain> Logger => _roomGrain._logger;
    private protected IGrainFactory GrainFactory => _roomGrain._grainFactory;
    private protected RoomConfig RoomConfig => _roomGrain._roomConfig;
    private protected WiredConfig WiredConfig => _roomGrain._wiredConfig;

    /// <summary>The room's monotonic clock in milliseconds (<see cref="RoomGrain.NowMs"/>).</summary>
    protected long NowMs() => _roomGrain.NowMs();

    /// <summary>The time of day by the room's clock (<see cref="RoomGrain.UtcNow"/>).</summary>
    protected DateTimeOffset UtcNow => _roomGrain.UtcNow;

    protected RoomEventModule EventModule => _roomGrain.EventModule;
    protected RoomSecurityModule SecurityModule => _roomGrain.SecurityModule;
    protected RoomModerationModule ModerationModule => _roomGrain.ModerationModule;
    protected RoomEntryModule EntryModule => _roomGrain.EntryModule;
    protected RoomMapModule MapModule => _roomGrain.MapModule;
    protected RoomObjectModule ObjectModule => _roomGrain.ObjectModule;
    protected RoomAvatarModule AvatarModule => _roomGrain.AvatarModule;
    protected RoomFurniModule FurniModule => _roomGrain.FurniModule;
    protected RoomActionModule ActionModule => _roomGrain.ActionModule;
    protected RoomPetModule PetModule => _roomGrain.PetModule;
    protected RoomBotModule BotModule => _roomGrain.BotModule;
    protected RoomTradeModule TradeModule => _roomGrain.TradeModule;

    protected RoomPathingSystem PathingSystem => _roomGrain.PathingSystem;
    protected RoomAvatarTickSystem AvatarTickSystem => _roomGrain.AvatarTickSystem;
    protected RoomPetTickSystem PetTickSystem => _roomGrain.PetTickSystem;
    protected RoomBotTickSystem BotTickSystem => _roomGrain.BotTickSystem;
    protected RoomRollerSystem RollerSystem => _roomGrain.RollerSystem;
    protected RoomWiredSystem WiredSystem => _roomGrain.WiredSystem;
    protected RoomGameSystem GameSystem => _roomGrain.GameSystem;
    protected RoomVariableFxSystem VariableFxSystem => _roomGrain.VariableFxSystem;
    protected RoomChatSystem ChatSystem => _roomGrain.ChatSystem;
    protected RoomCommandSystem CommandSystem => _roomGrain.CommandSystem;
    protected RoomTimerSystem TimerSystem => _roomGrain.TimerSystem;
    protected RoomWordQuizSystem WordQuizSystem => _roomGrain.WordQuizSystem;
    protected RoomWiredChestSystem WiredChestSystem => _roomGrain.WiredChestSystem;
    protected RoomWiredTransactionSystem WiredTransactionSystem =>
        _roomGrain.WiredTransactionSystem;
    protected RoomVariableLeaderboardSystem VariableLeaderboardSystem =>
        _roomGrain.VariableLeaderboardSystem;
    protected RoomWaterAreaSystem WaterAreaSystem => _roomGrain.WaterAreaSystem;
}
