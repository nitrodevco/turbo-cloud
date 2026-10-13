using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>A room's moderation summary, for the room tool.</summary>
[GenerateSerializer, Immutable]
public sealed record ModeratorRoomInfoEventMessageComposer : IComposer
{
    [Id(0)]
    public required ModeratorRoomInfoSnapshot Room { get; init; }
}
