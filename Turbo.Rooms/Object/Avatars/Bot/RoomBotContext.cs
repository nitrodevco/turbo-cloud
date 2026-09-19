using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Avatars.Bot;

public sealed class RoomBotContext(RoomGrain roomGrain, IRoomBot roomObject)
    : RoomAvatarContext<IRoomBot, IRoomBotLogic, IRoomBotContext>(roomGrain, roomObject),
        IRoomBotContext
{
    IRoomBot IRoomBotContext.RoomObject => RoomObject;
}
