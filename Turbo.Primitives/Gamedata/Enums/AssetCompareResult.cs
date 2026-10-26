namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>What comparing the copy of a file a publish target holds with the hotel's found.</summary>
public enum AssetCompareResult
{
    /// <summary>They differ in what players get: a window, a text, an image or other data.</summary>
    Changed = 0,

    /// <summary>
    /// Only the client build stamp they were packed from differs (<c>"version":"99_classic-js-..."</c>):
    /// sending it changes nothing for players.
    /// </summary>
    StampOnly = 1,

    /// <summary>They hold the same: the hotel's copy was packed again with nothing changed in it.</summary>
    Same = 2,

    /// <summary>The target holds no such file.</summary>
    NotOnTarget = 3,

    /// <summary>The target's copy could not be read, or is not a bundle; the comparison says why.</summary>
    Unreadable = 4,
}
