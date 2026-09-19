namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a present keeps in its extra data under <see cref="SECTION"/>: the wrapped furniture
/// row, owned by the same player and in no room.
/// </summary>
public sealed record PresentStorage
{
    public const string SECTION = "gift";

    public required int ItemId { get; init; }
}
