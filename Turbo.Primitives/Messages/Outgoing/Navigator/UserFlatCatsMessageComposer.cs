using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

[GenerateSerializer, Immutable]
public sealed record UserFlatCatsMessageComposer : IComposer
{
    [Id(0)]
    public List<object>? Nodes { get; init; }
}
