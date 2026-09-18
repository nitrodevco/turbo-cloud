namespace Turbo.Primitives.Furniture;

/// <summary>What a present keeps in its extra data: the wrapped furniture row.</summary>
public sealed record PresentStorage
{
    public required int ItemId { get; init; }
}
