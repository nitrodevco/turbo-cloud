namespace Turbo.Primitives.Furniture;

/// <summary>
/// Habbo wheel state values as the client's <c>FurnitureHabboWheelVisualization</c> reads them:
/// -1 plays the spinning animation, 1..10 lands on that segment.
/// </summary>
public static class WheelStates
{
    public const int SPINNING = -1;
    public const int MIN_VALUE = 1;
    public const int MAX_VALUE = 10;
}
