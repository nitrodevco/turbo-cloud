namespace Turbo.Admin.Api.Contracts;

/// <summary>A plan item a page builder did not make, by its key, and why in words for the editor.</summary>
public sealed record CatalogBuildFailure(string Key, string Error);
