using Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object;

internal abstract class RoomObjectContext(RoomGrain room) : IRoomObjectContext
{
    protected RoomGrain _room = room;

    public IRoomGrain Room => _room;

    public RoomId RoomId => (RoomId)Room.GetPrimaryKeyLong();

    public abstract RoomObjectId ObjectId { get; }
}
