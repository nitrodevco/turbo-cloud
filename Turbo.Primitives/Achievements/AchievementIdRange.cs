namespace Turbo.Primitives.Achievements;

/// <summary>An inclusive range of achievement ids a pack owns.</summary>
public readonly record struct AchievementIdRange(int First, int Last)
{
    public bool Contains(int id) => id >= First && id <= Last;

    public bool Overlaps(AchievementIdRange other) => First <= other.Last && other.First <= Last;
}
