using Turbo.Primitives.Rooms.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Rooms.Logic.Avatar;

public class MovingAvatarLogic(IRoomAvatarContext ctx)
    : RoomObjectLogicBase<IRoomAvatarContext>(ctx),
        IMovingAvatarLogic { }
