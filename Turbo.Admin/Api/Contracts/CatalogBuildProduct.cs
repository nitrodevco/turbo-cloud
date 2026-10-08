namespace Turbo.Admin.Api.Contracts;

/// <summary>What a built offer gives: the product type as the panel names it, the item, its extra parameter and how many.</summary>
public sealed record CatalogBuildProduct(
    string Type,
    int? DefinitionId,
    string? DefinitionName,
    string? ExtraParam,
    int Quantity
);
