using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

[GenerateSerializer, Immutable]
public sealed record NavigatorSearchResultBlocksMessageComposer : IComposer
{
    [Id(0)]
    public required string SearchCodeOriginal { get; init; }

    [Id(1)]
    public required string FilteringData { get; init; }

    [Id(2)]
    public required ImmutableArray<NavigatorSearchResultBlockSnapshot> Blocks { get; init; }
}
