namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>How a stack combines its conditions. Modes past <see cref="All"/> come from the
/// "condition evaluation" addon (eval_mode.0..6) and the counted ones read the addon threshold.</summary>
public enum WiredConditionModeType
{
    None = 0,
    Any = 1,
    All = 2,
    NoneMatch = 3,
    NotAll = 4,
    AtLeast = 5,
    AtMost = 6,
    Exactly = 7,
}
