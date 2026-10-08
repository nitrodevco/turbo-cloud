namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What a page builder would do to a page: the layout the built page wants, whether it makes a
/// page per item (pets), the items it would add or move, and anything worth knowing first.
/// </summary>
public sealed record CatalogBuildPlan(
    string Builder,
    string Layout,
    bool CreatesPages,
    CatalogBuildItem[] Items,
    string[] Warnings
);
