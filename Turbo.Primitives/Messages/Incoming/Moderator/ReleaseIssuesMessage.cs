using System.Collections.Immutable;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>ReleaseIssuesMessageComposer(issueIds)</c>.</summary>
public record ReleaseIssuesMessage : IMessageEvent
{
    public required ImmutableArray<int> IssueIds { get; init; }
}
