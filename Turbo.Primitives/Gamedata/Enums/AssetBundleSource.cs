namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>Where a bundle came from: converted from Habbo's file by a sync, or uploaded by staff.</summary>
public enum AssetBundleSource
{
    Habbo = 0,

    /// <summary>Uploaded in the panel. A sync leaves it alone, so a hotel's own edit is not overwritten.</summary>
    Upload = 1,
}
