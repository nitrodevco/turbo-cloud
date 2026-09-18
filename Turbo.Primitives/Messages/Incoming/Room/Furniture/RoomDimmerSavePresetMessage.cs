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
    public required RoomObjectId ObjectId { get; init; }
}
