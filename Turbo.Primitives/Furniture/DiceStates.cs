namespace Turbo.Primitives.Furniture;

/// <summary>
/// Dice state values as the client's <c>FurnitureDiceLogic</c> reads them: 0 is a closed dice,
/// 100 is rolling, and 1..6 is a face. Anything else renders as closed.
/// </summary>
public static class DiceStates
{
    public const int OFF = 0;
    public const int ROLLING = 100;
    public const int MIN_VALUE = 1;
    public const int MAX_VALUE = 6;
}
