using System.Collections.Immutable;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>CloseIssuesMessageComposer(issueIds, closeReason)</c>: the reason comes first on the wire, then the ids.</summary>
public record CloseIssuesMessage : IMessageEvent
{
    public required int CloseReason { get; init; }

    public required ImmutableArray<int> IssueIds { get; init; }
}
