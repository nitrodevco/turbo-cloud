namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// How a stack combines its conditions. Everything past <see cref="All"/> comes from the
/// "condition evaluation" addon and mirrors the choices its editor offers
/// (<c>eval_mode.0..3</c> and the three counted ones, <c>eval_mode.cmp.0..2</c>), which are the
/// only combinations the client can save.
/// </summary>
public enum WiredConditionModeType
{
    None = 0,

    /// <summary>"At least one".</summary>
    Any = 1,
    All = 2,

    /// <summary>"None".</summary>
    NoneMatch = 3,
    NotAll = 4,

    /// <summary>"Less than" the addon's count.</summary>
    LessThan = 5,

    /// <summary>"Exactly" the addon's count.</summary>
    Exactly = 6,

    /// <summary>"More than" the addon's count.</summary>
    MoreThan = 7,
}
