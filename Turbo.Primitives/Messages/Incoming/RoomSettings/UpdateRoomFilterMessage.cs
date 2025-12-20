using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record UpdateRoomFilterMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
    public bool IsAddingWord { get; init; }
    public string Word { get; init; } = string.Empty;
}
