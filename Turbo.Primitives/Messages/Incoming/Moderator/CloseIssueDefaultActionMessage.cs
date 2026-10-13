using System.Collections.Immutable;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary>
/// <c>CloseIssueDefaultActionMessageComposer(issueId, otherIssueIds, topicId)</c>: the bundle's
/// highest priority issue, the rest of the bundle, and the topic (-1: the issue's own).
/// </summary>
public record CloseIssueDefaultActionMessage : IMessageEvent
{
    public required int IssueId { get; init; }

    public required ImmutableArray<int> OtherIssueIds { get; init; }

    public required int TopicId { get; init; }
}
