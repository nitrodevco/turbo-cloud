using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record IgnoredUsersMessageComposer : IComposer
{
    [Id(0)]
    public required List<PlayerId> IgnoredUserIds { get; init; }
}
