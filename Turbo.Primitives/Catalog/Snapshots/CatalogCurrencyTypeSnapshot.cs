using Orleans;

namespace Turbo.Primitives.Catalog.Snapshots;

[GenerateSerializer, Immutable]
public sealed record CatalogCurrencyTypeSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string CurrencyKey { get; init; }

    [Id(2)]
    public required bool IsActivityPoints { get; init; }

    [Id(3)]
    public int? ActivityPointType { get; init; }

    [Id(4)]
    public required bool Enabled { get; init; }
}
