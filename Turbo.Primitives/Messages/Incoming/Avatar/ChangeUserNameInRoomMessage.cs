using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Avatar;

/// <summary>The name a player with a name change chose from their own avatar menu in a room.</summary>
public record ChangeUserNameInRoomMessage : IMessageEvent
{
    public required string Name { get; init; }
}
