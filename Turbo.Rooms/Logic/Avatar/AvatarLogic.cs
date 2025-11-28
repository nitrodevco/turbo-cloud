using Turbo.Primitives.Rooms.Avatars;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Logic.Avatar;

[RoomObjectLogic("default_avatar")]
public class AvatarLogic(IRoomAvatarContext ctx) : MovingAvatarLogic(ctx) { }
