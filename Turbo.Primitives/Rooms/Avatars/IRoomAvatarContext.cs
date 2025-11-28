using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Avatars;

public interface IRoomAvatarContext : IRoomObjectContext
{
    public IRoomAvatar Avatar { get; }
}
