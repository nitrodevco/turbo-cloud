using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Snapshots.Rooms.Mapping;

namespace Turbo.Primitives.Snapshots.Rooms;

public sealed record RoomModelSnapshot(
    int Id,
    string Name,
    int DoorX,
    int DoorY,
    Rotation DoorRotation,
    CompiledRoomModelSnapshot Model
);
