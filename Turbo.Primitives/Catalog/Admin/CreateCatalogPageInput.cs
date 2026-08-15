using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Catalog.Admin;

[GenerateSerializer, Immutable]
public sealed record CreateCatalogPageInput
{
    [Id(0)]
    public int? ParentId { get; init; }

    [Id(1)]
    public required string Localization { get; init; }

    [Id(2)]
    public string? Name { get; init; }

    [Id(3)]
    public required int Icon { get; init; }

    [Id(4)]
    public required string Layout { get; init; }

    [Id(5)]
    public List<string>? ImageData { get; init; }

    [Id(6)]
    public List<string>? TextData { get; init; }

    [Id(7)]
    public required bool Visible { get; init; }
}
