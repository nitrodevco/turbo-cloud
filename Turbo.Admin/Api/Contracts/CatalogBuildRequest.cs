namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A page builder run, to preview or apply: which builder, and what it builds from (a colour
/// family, a furni line or a class-name prefix, a pet type). Applying also names the plan items
/// to make, the price and flags each new offer gets, whether the page takes the builder's layout,
/// and which catalogs show the pages the pet builder makes. With <see cref="NewPageTitle"/>, the
/// page builds onto a new page of that title (and <see cref="NewPageIcon"/>), made under the page
/// named in the address with the builder's layout and shown as <see cref="Display"/> says.
/// </summary>
public sealed record CatalogBuildRequest(
    string? Builder,
    string? Base,
    string? Line,
    string? Prefix,
    int? PetType,
    string[]? Keys,
    int CostCredits,
    int CostCurrency,
    int? CurrencyTypeId,
    int ClubLevel,
    bool CanGift,
    bool Visible,
    bool SetLayout,
    string? Display,
    string? NewPageTitle = null,
    int? NewPageIcon = null
);
