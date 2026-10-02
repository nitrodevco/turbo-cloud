namespace Turbo.Primitives.Achievements;

/// <summary>How achievement ids are handed out, so a pack and a hotel never claim the same one.</summary>
public static class AchievementIds
{
    /// <summary>
    /// A hotel's own achievements, and any plugin's, start here. Ids below it belong to the packs
    /// that declare them (the Habbo catalog's ids come from its pack).
    /// </summary>
    public const int CUSTOM_START = 100000;

    /// <summary>The lowest id a pack may claim; lower ids are left unused for the client.</summary>
    public const int PACK_START = 1000;
}
