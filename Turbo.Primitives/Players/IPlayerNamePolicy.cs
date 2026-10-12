namespace Turbo.Primitives.Players;

/// <summary>
/// The hotel's own rules on names, over <see cref="PlayerNames"/>' fixed ones: the names no one
/// may take (<c>Turbo:Players:BlockedNames</c>). Asked wherever a player gets a name - the sign-up
/// and its suggestions, and the new user flow.
/// </summary>
public interface IPlayerNamePolicy
{
    /// <summary>Whether the hotel blocks this name.</summary>
    public bool IsBlocked(string name);
}
