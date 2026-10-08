namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// One offer a page builder would make or move: its key in the plan, the name staff know it by,
/// the name key it gets and what it gives; the title of its own page (pets), the offer moved
/// (sold-out limited), whether the catalog already sells the same thing, and a remark.
/// </summary>
public sealed record CatalogBuildItem(
    string Key,
    string Title,
    string LocalizationId,
    CatalogBuildProduct[] Products,
    string? PageTitle,
    int? OfferId,
    bool AlreadyOffered,
    string? Note
);
