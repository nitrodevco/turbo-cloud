using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record RelationshipStatusInfoEventMessageComposer : IComposer
{
    [Id(0)]
    public required PlayerId UserId { get; init; }

    [Id(1)]
    public required List<RelationshipStatusEntrySnapshot> Entries { get; init; }
}
