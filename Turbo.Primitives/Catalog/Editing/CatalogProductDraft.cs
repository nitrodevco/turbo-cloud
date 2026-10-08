using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// What an offer gives: a floor or wall item (<see cref="DefinitionId"/>, a furniture definition
/// of the same type), a badge (<see cref="ExtraParam"/>, its code), an effect (its id), a bot
/// (its figure, named by the definition when one is given), a pet (its type, or a definition
/// whose class name carries it), or a membership (<see cref="ProductType.HabboClub"/>, with
/// <see cref="Subscription"/> and <see cref="SubscriptionDays"/>); and how many.
/// </summary>
public sealed record CatalogProductDraft(
    ProductType Type,
    int? DefinitionId,
    string? ExtraParam,
    int Quantity,
    SubscriptionType? Subscription = null,
    int SubscriptionDays = 0
);
