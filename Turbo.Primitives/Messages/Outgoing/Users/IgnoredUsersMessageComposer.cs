using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record IgnoredUsersMessageComposer : IComposer
{
    [Id(0)]
    public required List<int> IgnoredUserIds { get; init; }
}
