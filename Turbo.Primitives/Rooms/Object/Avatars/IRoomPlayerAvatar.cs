using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomPlayerAvatar : IRoomAvatar
{
    public PlayerId PlayerId { get; }
}
