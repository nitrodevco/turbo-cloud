using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record FurnitureAliasesMessageComposer : IComposer
{
    [Id(0)]
    public required List<(string, string)> Aliases { get; init; }
}
