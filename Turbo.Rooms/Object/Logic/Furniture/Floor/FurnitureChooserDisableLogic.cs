using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The <c>:chooser</c> disabler (<c>conf_chooser_disable</c>). While one in the room is switched
/// on, the second flag of <c>ConfigurationItemStates</c> is set and the client opens the user
/// chooser only for players with room rights (Flash <c>ChatInputWidgetHandler</c>, through
/// <c>RoomEngine.activeRoomHasChooserDisabled</c>). The chooser is drawn from what the client
/// already holds, so there is nothing for the server to refuse.
/// </summary>
[RoomObjectLogic("chooser_disable")]
public class FurnitureChooserDisableLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureConfigurationControlLogic(stuffDataFactory, ctx);
