namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>What an asset job does.</summary>
public enum AssetJobKind
{
    /// <summary>Downloads Habbo's libraries that are missing or newer, and converts them to bundles.</summary>
    Sync = 0,

    /// <summary>Sends the bundles a publish target lacks, or holds an older copy of.</summary>
    Publish = 1,
}
