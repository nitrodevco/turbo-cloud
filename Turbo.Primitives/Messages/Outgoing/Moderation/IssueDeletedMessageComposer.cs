using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>A call for help gone from the queue (<c>IssueDeletedMessageParser</c> reads the id as a string).</summary>
[GenerateSerializer, Immutable]
public sealed record IssueDeletedMessageComposer : IComposer
{
    [Id(0)]
    public required int IssueId { get; init; }
}
