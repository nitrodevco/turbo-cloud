using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>ModTradingLockMessageComposer(userId, message, minutes, topicId, issueId)</c>: the mod action window's trade locks (<c>ModActionCtrl.as:288-291</c>, the action's hours times 60); the issue id only when there is one.</summary>
public record ModTradingLockMessage : IMessageEvent
{
    public required int UserId { get; init; }
    public required string Message { get; init; }
    public required int Minutes { get; init; }
    public required int TopicId { get; init; }

    /// <summary>-1 when the action is not taken from an issue.</summary>
    public required int IssueId { get; init; }
}
