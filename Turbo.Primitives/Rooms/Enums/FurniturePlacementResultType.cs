namespace Turbo.Primitives.Rooms.Enums;

/// <summary>
/// What became of a player placing or moving a furni. Each refusal tells the player its own
/// <c>furni_placement_error</c> text.
/// </summary>
public enum FurniturePlacementResultType
{
    Done = 0,

    /// <summary>The spot does not take the item: "Sorry, you cannot place this item here."</summary>
    Refused = 1,

    /// <summary>The player may not build here: "You don't have rights to put furniture in here!"</summary>
    NotAllowed = 2,

    /// <summary>The room holds as many of its kind as it may: "You have reached the limit of this type of item per room."</summary>
    TypeLimit = 3,
}
