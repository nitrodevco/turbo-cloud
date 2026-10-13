using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary>
/// <c>ModToolSanctionComposer(issueId, accountId, topicId)</c>: which default sanction a player would
/// get, for an issue's reported player (<c>IssueManager.requestSanctionData</c>, account -1) or
/// for an account (<c>requestSanctionDataForAccount</c>, issue -1); the topic is -1 for the issue's own.
/// </summary>
public record ModToolSanctionMessage : IMessageEvent
{
    public required int IssueId { get; init; }

    public required int AccountId { get; init; }

    public required int TopicId { get; init; }
}
