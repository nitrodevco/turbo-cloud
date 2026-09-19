using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>Shared numeric comparison for the wired boxes that carry a comparison radio.</summary>
public static class WiredComparison
{
    public static bool Compare(WiredComparisonType type, long value, long reference) =>
        type switch
        {
            WiredComparisonType.LessThan => value < reference,
            WiredComparisonType.Equals => value == reference,
            WiredComparisonType.GreaterThan => value > reference,
            WiredComparisonType.LessThanOrEquals => value <= reference,
            WiredComparisonType.NotEquals => value != reference,
            WiredComparisonType.GreaterTHanOrEquals => value >= reference,
            _ => false,
        };

    /// <summary>The three-way radio (comparison.0/1/2) used by altitude, clock and score boxes.</summary>
    public static bool CompareThreeWay(int type, long value, long reference) =>
        type switch
        {
            0 => value < reference,
            1 => value == reference,
            2 => value > reference,
            _ => false,
        };
}
