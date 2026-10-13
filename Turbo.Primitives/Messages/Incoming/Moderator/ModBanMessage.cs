using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>ModBanMessageComposer(userId, message, topicId, sanctionTypeId, avatarOnly, issueId)</c>: the mod action window's bans (<c>ModActionCtrl.as:277</c>); the issue id only when there is one.</summary>
public record ModBanMessage : IMessageEvent
{
    public required int UserId { get; init; }
    public required string Message { get; init; }
    public required int TopicId { get; init; }

    /// <summary>3 18 hours, 4 7 days, 5 and 7 30 days, 6 100 years (<c>ModActionCtrl.as:66-71</c>).</summary>
    public required int SanctionTypeId { get; init; }

    /// <summary>"Ban avatar-only 100 years" (action 106).</summary>
    public required bool AvatarOnly { get; init; }

    /// <summary>-1 when the action is not taken from an issue.</summary>
    public required int IssueId { get; init; }
}
