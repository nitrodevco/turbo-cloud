using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Primitives.Snapshots.Rooms.Mapping;

public sealed record RoomModelSnapshot(
    int Id,
    string Name,
    string Model,
    int DoorX,
    int DoorY,
    Rotation DoorRotation,
    CompiledRoomModelSnapshot CompiledModel
);
