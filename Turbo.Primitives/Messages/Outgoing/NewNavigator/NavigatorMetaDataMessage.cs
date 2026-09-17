using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

[GenerateSerializer, Immutable]
public sealed record NavigatorMetaDataMessage : IComposer
{
    [Id(0)]
    public required ImmutableArray<NavigatorTopLevelContextSnapshot> TopLevelContexts { get; init; }
}
