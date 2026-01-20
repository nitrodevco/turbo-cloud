using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Rooms.Object.Logic.Avatar;

[RoomObjectLogic("default_avatar")]
public sealed class PlayerLogic(IRoomPlayerContext ctx)
    : AvatarLogic<IRoomPlayer, IRoomPlayerLogic, IRoomPlayerContext>(ctx),
        IRoomPlayerLogic { }
