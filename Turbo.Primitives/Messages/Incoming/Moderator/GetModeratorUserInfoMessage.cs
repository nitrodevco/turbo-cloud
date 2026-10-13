using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>GetModeratorUserInfoMessageComposer(userId)</c>.</summary>
public record GetModeratorUserInfoMessage : IMessageEvent
{
    public required int UserId { get; init; }
}
