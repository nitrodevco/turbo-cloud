namespace Turbo.Furniture.Configuration;

public class FurnitureConfig
{
    public const string SECTION_NAME = "Turbo:Furniture";

    public float MinimumZValue { get; init; } = 0.001f;
}
