namespace Turbo.Primitives.Catalog.Editing;

/// <summary>What a publish put live: the pages and offers of both catalogs, and how many players were told.</summary>
public sealed record CatalogPublishResult(int Pages, int Offers, int PlayersTold);
