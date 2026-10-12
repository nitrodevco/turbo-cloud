using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Hotlooks;

[GenerateSerializer, Immutable]
public sealed record HotLooksMessageComposer : IComposer
{
    /// <summary>Each look's gender (<c>m</c> / <c>f</c>) and figure, in the order the client lists them.</summary>
    [Id(0)]
    public required List<(string Gender, string Figure)> Looks { get; init; }
}
