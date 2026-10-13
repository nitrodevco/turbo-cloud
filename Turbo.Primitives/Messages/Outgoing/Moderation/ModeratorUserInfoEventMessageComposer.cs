using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>A player's moderation summary, for the user info window.</summary>
[GenerateSerializer, Immutable]
public sealed record ModeratorUserInfoEventMessageComposer : IComposer
{
    [Id(0)]
    public required ModeratorUserInfoSnapshot User { get; init; }
}
