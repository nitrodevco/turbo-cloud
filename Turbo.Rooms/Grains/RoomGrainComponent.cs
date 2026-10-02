using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Wired.VariableFx;

namespace Turbo.Rooms.Grains;

/// <summary>
/// Anything that lives inside one room grain and works through its siblings: the modules, the
/// systems, and every object logic. It names the siblings the way the grain's own partials do,
/// so a call reads <c>ObjectModule.RemoveObjectAsync(...)</c> rather than
/// <c>_roomGrain.ObjectModule.RemoveObjectAsync(...)</c>. The grain itself stays reachable as
/// <see cref="_roomGrain"/> for its state, config and services.
/// <para>
/// A new module or system on <see cref="RoomGrain"/> gets its shorthand here as well; this is
/// the one list, so nothing else keeps its own.
/// </para>
/// </summary>
public abstract class RoomGrainComponent(RoomGrain roomGrain)
{
    protected readonly RoomGrain _roomGrain = roomGrain;

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
}
