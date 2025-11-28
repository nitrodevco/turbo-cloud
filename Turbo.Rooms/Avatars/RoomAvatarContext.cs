using Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Avatars;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Avatars;

internal class RoomAvatarContext(
    RoomGrain roomGrain,
    RoomAvatarModule avatarModule,
    IRoomAvatar roomAvatar
) : IRoomAvatarContext
{
    protected readonly RoomGrain _roomGrain = roomGrain;
    protected readonly RoomAvatarModule _avatarModule = avatarModule;

    public IRoomAvatar Avatar => roomAvatar;
    public RoomId RoomId => RoomId.From(_roomGrain.GetPrimaryKeyLong());
}
