namespace Turbo.Primitives.Furniture.Enums;

/// <summary>
/// Furniture actions the client sends as dedicated packets rather than a plain use. Each maps to
/// one incoming message; the item's logic decides whether it responds to it.
/// </summary>
public enum FurnitureInteractionType
{
    ThrowDice,
    DiceOff,
    SpinWheel,
    EnterOneWayDoor,
}
