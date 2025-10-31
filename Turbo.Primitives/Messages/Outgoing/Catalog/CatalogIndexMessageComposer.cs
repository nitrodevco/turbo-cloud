using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Snapshots.Catalog;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

public record CatalogIndexMessageComposer : IComposer
{
    public required CatalogSnapshot Catalog { get; init; }
    public bool NewAdditionsAvailable { get; init; }
}
