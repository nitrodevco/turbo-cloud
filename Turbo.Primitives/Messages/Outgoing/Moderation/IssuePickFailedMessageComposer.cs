using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>
/// The issues a pick did not get (<c>ModerationMessageHandler.onIssuePickFailed</c>); with retry
/// on, a client whose issues another moderator holds picks the next one itself, up to 10 times.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record IssuePickFailedMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<ModIssuePickFailureSnapshot> Issues { get; init; }

    [Id(1)]
    public required bool RetryEnabled { get; init; }

    [Id(2)]
    public required int RetryCount { get; init; }
}
