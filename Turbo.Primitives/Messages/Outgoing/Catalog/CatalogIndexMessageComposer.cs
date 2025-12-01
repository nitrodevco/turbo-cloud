using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record CatalogIndexMessageComposer : IComposer
{
    [Id(0)]
    public required CatalogSnapshot Catalog { get; init; }

    [Id(1)]
    public bool NewAdditionsAvailable { get; init; }
}
