using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>A call for help that is new or changed, to every moderator (<c>ModerationMessageHandler.onIssueInfo</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record IssueInfoMessageComposer : IComposer
{
    [Id(0)]
    public required ModIssueSnapshot Issue { get; init; }
}
