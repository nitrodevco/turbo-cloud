using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Object.Avatars;

internal class RoomAvatarContext(
    RoomGrain roomGrain,
    RoomAvatarModule avatarModule,
    IRoomAvatar roomAvatar
) : RoomObjectContext(roomGrain), IRoomAvatarContext
{
    protected readonly RoomAvatarModule _avatarModule = avatarModule;

    public IRoomAvatar Avatar => roomAvatar;
}
