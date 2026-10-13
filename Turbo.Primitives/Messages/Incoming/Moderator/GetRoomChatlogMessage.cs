using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>GetRoomChatlogMessageComposer(roomType, flatId)</c>.</summary>
public record GetRoomChatlogMessage : IMessageEvent
{
    /// <summary>0 for a guest room, 1 for a public one (<c>StartPanelCtrl.as:142</c>).</summary>
    public required int RoomType { get; init; }

    public required int RoomId { get; init; }
}
