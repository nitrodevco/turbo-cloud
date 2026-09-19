namespace Turbo.Rooms.Wired;

/// <summary>
/// Where an experience total stands in a level table: the level reached, and the experience at
/// which that level and the next one start. At the last level the two are the same.
/// </summary>
public readonly record struct WiredLevelProgress(
    int Level,
    int MaxLevel,
    long LevelStartXp,
    long NextLevelXp,
    bool IsMaxed
);
