using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Callforhelp;

/// <summary>The call for help categories and their topics, sent at login.</summary>
[GenerateSerializer, Immutable]
public sealed record CfhTopicsInitMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<CfhCategorySnapshot> Categories { get; init; }
}
