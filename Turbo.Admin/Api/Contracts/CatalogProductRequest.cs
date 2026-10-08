namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What an offer gives, as saved: <c>floor</c> or <c>wall</c> with the item, <c>badge</c> with
/// its code, <c>effect</c> with its id, <c>robot</c> with the bot's figure (and the item that
/// names it, if any), <c>pet</c> with its type (or an item named after it), or <c>club</c> with a
/// membership (<c>HabboClub</c>, <c>BuildersClub</c>) and its days; and how many.
/// </summary>
public sealed record CatalogProductRequest(
    string? Type,
    int? DefinitionId,
    string? ExtraParam,
    int Quantity,
    string? Subscription = null,
    int SubscriptionDays = 0
);
