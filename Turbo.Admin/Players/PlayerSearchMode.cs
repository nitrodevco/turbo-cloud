namespace Turbo.Admin.Players;

/// <summary>What the panel's player search matches its text against.</summary>
public enum PlayerSearchMode
{
    /// <summary>Part of the player's name.</summary>
    Name,

    /// <summary>The player's id, exactly.</summary>
    Id,

    /// <summary>Part of the Discord username they sign in with, or the Discord id exactly.</summary>
    Discord,
}
