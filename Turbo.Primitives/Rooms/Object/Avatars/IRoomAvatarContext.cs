using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomAvatarContext : IRoomObjectContext
{
    public IRoomAvatar Avatar { get; }
}
