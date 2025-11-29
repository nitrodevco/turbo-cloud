using System.Collections.Generic;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Rooms.Object.Logic.Avatar;

public class MovingAvatarLogic(IRoomAvatarContext ctx)
    : RoomObjectLogicBase<IRoomAvatarContext>(ctx),
        IMovingAvatarLogic { }
