using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Nux;

/// <summary>The starter room a new player picked: one of <c>new.user.flow.roomTypes</c> ("10").</summary>
public record SelectInitialRoomMessage : IMessageEvent
{
    public required string RoomType { get; init; }
}
