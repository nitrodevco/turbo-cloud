using Turbo.Primitives.Snapshots.Rooms.Mapping;
using Turbo.Rooms.Abstractions;

namespace Turbo.Rooms.Mapping;

public sealed class RoomMap(CompiledRoomModelSnapshot roomModelSnapshot) : IRoomMap
{
    private readonly CompiledRoomModelSnapshot _roomModelSnapshot = roomModelSnapshot;

    public int Width => _roomModelSnapshot.Width;
    public int Height => _roomModelSnapshot.Height;
}
