using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

[GenerateSerializer, Immutable]
public sealed record BlockListMessageComposer : IComposer
{
    [Id(0)]
    public required List<int> BlockedUserIds { get; init; }
}
