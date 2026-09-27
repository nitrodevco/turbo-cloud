using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Database.Extensions;

public static class RoomModelEntityExtensions
{
    /// <summary>
    /// A model row as the room reads it. The heightmap is cleaned and compiled by the room module,
    /// which knows the format, and handed in: <paramref name="modelData"/> is the cleaned text
    /// and <paramref name="compiled"/> what it compiled to.
    /// </summary>
    public static RoomModelSnapshot ToSnapshot(
        this RoomModelEntity entity,
        string modelData,
        CompiledRoomModelSnapshot compiled
    ) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Model = modelData,
            DoorX = entity.DoorX,
            DoorY = entity.DoorY,
            DoorRotation = entity.DoorRotation,
            Width = compiled.Width,
            Height = compiled.Height,
            Size = compiled.Width * compiled.Height,
            BaseHeights = compiled.Heights,
            BaseFlags = compiled.Flags,
        };
}
