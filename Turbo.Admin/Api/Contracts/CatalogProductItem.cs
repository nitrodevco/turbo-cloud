namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What an offer gives: its type (floor, wall, badge, effect, robot, club, pet), the furniture
/// definition and its class name and sprite for an item, the extra parameter, how many, the
/// limited series it is sold as, and a membership's type and days.
/// </summary>
public sealed record CatalogProductItem(
    int Id,
    string Type,
    int? DefinitionId,
    string? DefinitionName,
    int? SpriteId,
    string? ExtraParam,
    int Quantity,
    CatalogLimitedItem? Limited,
    string? SubscriptionType,
    int SubscriptionDays
);
