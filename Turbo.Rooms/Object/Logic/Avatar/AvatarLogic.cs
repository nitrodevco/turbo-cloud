using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Avatar;

[RoomObjectLogic("default_avatar")]
public class AvatarLogic(IRoomAvatarContext ctx) : MovingAvatarLogic(ctx) { }
