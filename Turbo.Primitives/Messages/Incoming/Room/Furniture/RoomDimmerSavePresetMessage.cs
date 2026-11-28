using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record RoomDimmerSavePresetMessage : IMessageEvent
{
    public required int PresetId { get; init; }
    public required int EffectType { get; init; }
    public required string Color { get; init; }
    public required int Brightness { get; init; }
    public required bool Apply { get; init; }

    /// <summary>
    /// Whether furni keep their own lights under the preset. A client older than build 116
    /// always writes false here.
    /// </summary>
    public required bool KeepFurnitureLights { get; init; }
    public required RoomObjectId ObjectId { get; init; }
}
