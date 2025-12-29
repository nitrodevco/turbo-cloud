using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Primitives.Rooms.Object;

public interface IRoomObjectContext
{
    public IRoomGrain Room { get; }
    public RoomObjectId ObjectId { get; }
    public RoomId RoomId { get; }
}
