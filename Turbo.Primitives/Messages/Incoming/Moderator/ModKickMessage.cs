using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>ModKickMessageComposer(userId, message, topicId, issueId)</c>: the mod action window's kick; the issue id only when there is one.</summary>
public record ModKickMessage : IMessageEvent
{
    public required int UserId { get; init; }

    public required string Message { get; init; }

    public required int TopicId { get; init; }

    /// <summary>-1 when the action is not taken from an issue.</summary>
    public required int IssueId { get; init; }
}
