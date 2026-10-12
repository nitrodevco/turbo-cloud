namespace Turbo.Primitives.Players;

/// <summary>
/// The result codes of <c>CheckUserNameResult</c> and <c>ChangeUserNameResult</c>, as Habbo sends
/// them (recorded 2026-10-12): a taken name is 4 with three suggestions, and a player who already
/// chose their name gets 6 for any other name.
/// </summary>
public enum NameCheckResult
{
    Available = 0,
    TooShort = 2,
    TooLong = 3,
    NotAllowed = 4,
    Taken = 4,
    ChangeNotAllowed = 6,
}
