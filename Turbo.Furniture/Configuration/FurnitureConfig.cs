namespace Turbo.Furniture.Configuration;

public class FurnitureConfig
{
    public const string SECTION_NAME = "Turbo:Furniture";

    public double MinimumZValue { get; init; } = 0.001;
}
