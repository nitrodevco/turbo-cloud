namespace Turbo.Primitives.Furniture;

/// <summary>
/// What a player is told when the room refuses to put their furni where they dropped it. The stock
/// hotel data configures <c>notification.furni_placement_error</c> as a bubble, and the client
/// reads its text from the <c>message</c> parameter, which names the hotel text to show.
/// </summary>
public static class FurniturePlacementNotifications
{
    /// <summary>The spot does not take the item: a placement or a move that was refused.</summary>
    public const string PLACEMENT_ERROR = "furni_placement_error";

    /// <summary>The <c>message</c> parameter: "Sorry, you cannot place this item here."</summary>
    public const string CANT_SET_ITEM_MESSAGE = "${room.error.cant_set_item}";
}
