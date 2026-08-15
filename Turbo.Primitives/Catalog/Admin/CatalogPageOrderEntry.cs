using Orleans;

namespace Turbo.Primitives.Catalog.Admin;

[GenerateSerializer, Immutable]
public sealed record CatalogPageOrderEntry
{
    [Id(0)]
    public required int PageId { get; init; }

    [Id(1)]
    public int? ParentId { get; init; }

    [Id(2)]
    public required int SortOrder { get; init; }
}
