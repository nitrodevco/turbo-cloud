namespace Turbo.Primitives.Rooms.Avatars;

public interface IRoomPlayerAvatar : IRoomAvatar
{
    public long PlayerId { get; }
}
