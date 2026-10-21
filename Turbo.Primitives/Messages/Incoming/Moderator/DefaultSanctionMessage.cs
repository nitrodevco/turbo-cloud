using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>DefaultSanctionMessageComposer(userId, topicId, message, issueId)</c>: the mod action window's "Default sanction" (<c>ModActionCtrl.as:214-231</c>); the issue id only when there is one.</summary>
public record DefaultSanctionMessage : IMessageEvent
{
    public required int UserId { get; init; }
    public required int TopicId { get; init; }
    public required string Message { get; init; }

    /// <summary>-1 when the action is not taken from an issue.</summary>
    public required int IssueId { get; init; }
}
