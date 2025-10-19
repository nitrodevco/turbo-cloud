using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record UpdateRoomFilterMessage : IMessageEvent
{
    public int RoomId { get; init; }
    public bool IsAddingWord { get; init; }
    public string Word { get; init; } = string.Empty;
}
