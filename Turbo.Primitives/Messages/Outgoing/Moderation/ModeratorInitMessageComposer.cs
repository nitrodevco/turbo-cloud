using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>Opens the moderation tool's start panel (<c>ModerationMessageHandler.onModeratorInit</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record ModeratorInitMessageComposer : IComposer
{
    [Id(0)]
    public required ModeratorInitSnapshot Init { get; init; }
}
