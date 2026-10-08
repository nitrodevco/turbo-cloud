namespace Turbo.Admin.Api.Contracts;

/// <summary>Every furni line the furniture is in, by name, for the furni line builder.</summary>
public sealed record CatalogFurniLinesResponse(CatalogFurniLine[] Lines);
