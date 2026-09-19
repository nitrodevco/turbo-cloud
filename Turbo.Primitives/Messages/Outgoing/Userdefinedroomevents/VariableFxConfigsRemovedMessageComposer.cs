using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;

/// <summary>Drops variable fx configs, and with them every status the client shows for them.</summary>
[GenerateSerializer, Immutable]
public sealed record VariableFxConfigsRemovedMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<int> ConfigIds { get; init; }
}
