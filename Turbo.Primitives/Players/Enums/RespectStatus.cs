namespace Turbo.Primitives.Players.Enums;

/// <summary>
/// Where a respect stands. It is written as pending before either player is touched, so a crash
/// part way through is finished by the recovery service rather than lost or done twice.
/// </summary>
public enum RespectStatus : byte
{
    /// <summary>Started: the giver may or may not have spent it yet.</summary>
    Pending = 0,

    /// <summary>Spent by the giver and received.</summary>
    Given = 1,

    /// <summary>The giver had none left today, so nothing was spent or received.</summary>
    Refused = 2,
}
