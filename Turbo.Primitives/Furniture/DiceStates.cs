namespace Turbo.Primitives.Furniture;

/// <summary>
/// Dice state values as the client renders them: 0 is a closed dice, -1 plays the rolling
/// animation, and 1..6 is a face.
/// </summary>
public static class DiceStates
{
    public const int OFF = 0;
    public const int ROLLING = -1;
    public const int MIN_VALUE = 1;
    public const int MAX_VALUE = 6;
}
