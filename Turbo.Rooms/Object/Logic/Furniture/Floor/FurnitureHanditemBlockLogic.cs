using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The Handitem blocker (<c>conf_handitem_block</c>). While one in the room is switched on, the
/// first flag of <c>ConfigurationItemStates</c> is set and clients stop offering to give or drop
/// a hand item (Flash <c>AvatarMenuView</c> and <c>OwnAvatarMenuView</c>, through
/// <c>RoomEngine.activeRoomHasHanditemControlBlocked</c>). The room refuses a hand item passed
/// from one player to another as well (<c>RoomAvatarModule.PassHandItemAsync</c>), since a
/// client need not hide a button to send the packet.
/// </summary>
[RoomObjectLogic("handitem_block")]
public class FurnitureHanditemBlockLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureConfigurationControlLogic(stuffDataFactory, ctx);
