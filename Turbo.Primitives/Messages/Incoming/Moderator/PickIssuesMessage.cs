using System.Collections.Immutable;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>PickIssuesMessageComposer(issueIds, retryEnabled, retryCount, message)</c>: the message is why the client picked, a log text.</summary>
public record PickIssuesMessage : IMessageEvent
{
    public required ImmutableArray<int> IssueIds { get; init; }

    public required bool RetryEnabled { get; init; }

    public required int RetryCount { get; init; }

    public required string Message { get; init; }
}
