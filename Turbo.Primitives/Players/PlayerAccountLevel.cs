namespace Turbo.Primitives.Players;

/// <summary>
/// The level a player's profile shows (<c>ExtendedProfile.accountLevel</c>; official client
/// capture 2026-10-14: the profile's "Level" 3 is what wired's <c>@level</c> reads, not the
/// achievement level). This hotel has no account levels to earn, so every player has this one,
/// in the profile and in wired alike.
/// </summary>
public static class PlayerAccountLevel
{
    public const int DEFAULT = 1;
}
