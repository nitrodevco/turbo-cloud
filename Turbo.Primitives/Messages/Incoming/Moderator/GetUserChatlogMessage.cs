using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>GetUserChatlogMessageComposer(userId)</c>.</summary>
public record GetUserChatlogMessage : IMessageEvent
{
    public required int UserId { get; init; }
}
