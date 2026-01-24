using Turbo.Primitives.Rooms.Object;

namespace Turbo.Furniture.Configuration;

public class FurnitureConfig
{
    public const string SECTION_NAME = "Turbo:Furniture";

    public Altitude MinimumZValue { get; init; } = Altitude.FromValue(0.01);
}
