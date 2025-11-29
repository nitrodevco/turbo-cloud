namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomPlayerAvatar : IRoomAvatar
{
    public long PlayerId { get; }
}
