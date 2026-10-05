namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What an offer gives, as saved: <c>floor</c>, <c>wall</c> or <c>badge</c> with the item or badge
/// code, or <c>club</c> with a membership (<c>HabboClub</c>, <c>BuildersClub</c>) and its days;
/// and how many.
/// </summary>
public sealed record CatalogProductRequest(
    string? Type,
    int? DefinitionId,
    string? ExtraParam,
    int Quantity,
    string? Subscription = null,
    int SubscriptionDays = 0
);
