using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Callforhelp;

/// <summary>The sanctions in force on the player asking; none is an empty list.</summary>
[GenerateSerializer, Immutable]
public sealed record SanctionStatusEventMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<SanctionStatusSnapshot> Sanctions { get; init; }
}
