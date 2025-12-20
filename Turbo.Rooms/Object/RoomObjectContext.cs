using Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object;

internal abstract class RoomObjectContext(RoomGrain roomGrain) : IRoomObjectContext
{
    protected readonly RoomGrain _roomGrain = roomGrain;

    public RoomId RoomId => (int)_roomGrain.GetPrimaryKeyLong();

    public abstract RoomObjectId ObjectId { get; }
}
