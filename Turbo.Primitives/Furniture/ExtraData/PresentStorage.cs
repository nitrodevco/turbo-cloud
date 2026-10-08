namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a present keeps in its extra data under <see cref="SECTION"/>: the box style and the
/// ribbon it was wrapped in, both 0 for the free box. The wrapped item is not named here: it is
/// the furniture row held by the present (its <c>chest_item_id</c>), which keeps it out of every
/// inventory until the present is opened, and returns it to its owner if the present is ever
/// deleted unopened.
/// </summary>
public sealed record PresentStorage
{
    public const string SECTION = "gift";

    /// <summary>How the client splits <see cref="GetObjectExtra"/> back into box and ribbon.</summary>
    private const int BOX_FACTOR = 1000;

    public required int BoxType { get; init; }
    public required int RibbonType { get; init; }

    /// <summary>
    /// The item's <c>extra</c> number, which the client's gift visualization reads as
    /// <c>box * 1000 + ribbon</c> to pick the box frame and the ribbon frame.
    /// </summary>
    public int GetObjectExtra() => (BoxType * BOX_FACTOR) + RibbonType;
}
