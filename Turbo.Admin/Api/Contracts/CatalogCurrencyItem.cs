namespace Turbo.Admin.Api.Contracts;

/// <summary>A currency an offer's second price can be in: an activity-point currency.</summary>
public sealed record CatalogCurrencyItem(int Id, string Name, int ActivityPointType);
