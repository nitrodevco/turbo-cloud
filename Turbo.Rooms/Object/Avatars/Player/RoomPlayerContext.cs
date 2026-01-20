using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Avatars.Player;

public sealed class RoomPlayerContext(RoomGrain roomGrain, IRoomPlayer roomObject)
    : RoomAvatarContext<IRoomPlayer, IRoomPlayerLogic, IRoomPlayerContext>(roomGrain, roomObject),
        IRoomPlayerContext { }
