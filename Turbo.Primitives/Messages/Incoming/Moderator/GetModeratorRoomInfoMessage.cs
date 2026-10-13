using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>GetModeratorRoomInfoMessageComposer(flatId)</c>.</summary>
public record GetModeratorRoomInfoMessage : IMessageEvent
{
    public required int RoomId { get; init; }
}
