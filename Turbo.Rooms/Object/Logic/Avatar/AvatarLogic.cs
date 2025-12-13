using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;

namespace Turbo.Rooms.Object.Logic.Avatar;

[RoomObjectLogic("default_avatar")]
public class AvatarLogic(IRoomAvatarContext ctx)
    : RoomObjectLogicBase<IRoomAvatarContext>(ctx),
        IRoomAvatarLogic
{
    public IRoomAvatarContext Context => _ctx;

    public bool CanRoll()
    {
        if (_ctx.Avatar.IsWalking)
            return false;

        return true;
    }
}
