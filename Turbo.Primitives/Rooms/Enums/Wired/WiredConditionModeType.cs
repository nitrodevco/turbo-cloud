namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// How a stack combines its conditions. Everything past <see cref="All"/> comes from the
/// "condition evaluation" addon and mirrors the choices its editor offers
/// (<c>eval_mode.0..3</c> and the three counted ones, <c>eval_mode.cmp.0..2</c>), then the counted
/// operators Habbo also runs though its editor offers none of them: a counted mode of 3 to 5
/// compares with "at most", "not" and "at least", and any higher one never passes (official client
/// capture WIN63-202610201032, 2026-10-23T00-05-33 / 00-06-44 / 00-07-24-UpdateAddon).
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

    /// <summary>At most the addon's count (counted mode 3).</summary>
    AtMost = 8,

    /// <summary>Anything but the addon's count (counted mode 4).</summary>
    NotExactly = 9,

    /// <summary>At least the addon's count (counted mode 5).</summary>
    AtLeast = 10,

    /// <summary>A counted mode past the operators: the conditions never pass.</summary>
    Never = 11,
}
